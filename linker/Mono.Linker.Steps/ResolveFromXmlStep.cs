//
// ResolveFromXmlStep.cs
//
// Author:
//   Jb Evain (jbevain@gmail.com)
//
// (C) 2006 Jb Evain
// (C) 2007 Novell, Inc.
// Copyright 2013 Xamarin Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.XPath;

using Mono.Cecil;

namespace Mono.Linker.Steps {

	public class XmlResolutionException : Exception {
		public XmlResolutionException (string message, Exception innerException)
			: base (message, innerException)
		{
		}
	}

	public class ResolveFromXmlStep : ResolveStep {

		static readonly string _signature = "signature";
		static readonly string _fullname = "fullname";
		static readonly string _required = "required";
		static readonly string _preserve = "preserve";
		static readonly string _accessors = "accessors";
		static readonly string _ns = string.Empty;

		static readonly string[] _accessorsAll = new string[] { "all" };
		static readonly char[] _accessorsSep = new char[] { ';' };

		XPathDocument _document;
		string _xmlDocumentLocation;

		public ResolveFromXmlStep (XPathDocument document, string xmlDocumentLocation = "<unspecified>")
		{
			_document = document;
			_xmlDocumentLocation = xmlDocumentLocation;
		}

		protected override void Process ()
		{
			XPathNavigator nav = _document.CreateNavigator ();
			nav.MoveToFirstChild ();

			// This step can be created with XML files that aren't necessarily
			// linker descriptor files. So bail if we don't have a <linker> element.
			if (nav.LocalName != "linker")
				return;

			try {
				ProcessAssemblies (Context, nav.SelectChildren ("assembly", _ns));
			} catch (Exception ex) {
				throw new XmlResolutionException (string.Format ("Failed to process XML description: {0}", _xmlDocumentLocation), ex);
			}
		}

		void ProcessAssemblies (LinkContext context, XPathNodeIterator iterator)
		{
			while (iterator.MoveNext ()) {
				AssemblyDefinition assembly = GetAssembly (context, GetFullName (iterator.Current));
				if (GetTypePreserve (iterator.Current) == TypePreserve.All) {
					foreach (var type in assembly.MainModule.Types)
						MarkAndPreserveAll (type);
				} else {
					ProcessTypes (assembly, iterator.Current.SelectChildren ("type", _ns));
					ProcessNamespaces (assembly, iterator.Current.SelectChildren ("namespace", _ns));
				}
			}
		}

		void ProcessNamespaces (AssemblyDefinition assembly, XPathNodeIterator iterator)
		{
			while (iterator.MoveNext ()) {
				string fullname = GetFullName (iterator.Current);
				foreach (TypeDefinition type in assembly.MainModule.Types) {
					if (type.Namespace != fullname)
						continue;

					MarkAndPreserveAll (type);
				}
			}
		}

		void MarkAndPreserveAll (TypeDefinition type)
		{
			Annotations.Mark (type);
			Annotations.SetPreserve (type, TypePreserve.All);

			if (!type.HasNestedTypes)
				return;

			foreach (TypeDefinition nested in type.NestedTypes)
				MarkAndPreserveAll (nested);
		}

		void ProcessTypes (AssemblyDefinition assembly, XPathNodeIterator iterator)
		{
			while (iterator.MoveNext ()) {
				XPathNavigator nav = iterator.Current;
				string fullname = GetFullName (nav);

				if (IsTypePattern (fullname)) {
					ProcessTypePattern (fullname, assembly, nav);
					continue;
				}

				TypeDefinition type = assembly.MainModule.GetType (fullname);

				if (type == null) {
					if (assembly.MainModule.HasExportedTypes) {
						foreach (var exported in assembly.MainModule.ExportedTypes) {
							if (fullname == exported.FullName) {
								Annotations.Mark (exported);
								Annotations.Mark (assembly.MainModule);
								var resolvedExternal = exported.Resolve ();
								if (resolvedExternal != null) {
									type = resolvedExternal;
									break;
								}
							}
						}
					}
				}

				if (type == null)
					continue;

				ProcessType (type, nav);
			}
		}

		static bool IsTypePattern (string fullname)
		{
			return fullname.IndexOf ("*") != -1;
		}

		static Regex CreateRegexFromPattern (string pattern)
		{
			return new Regex (pattern.Replace (".", @"\.").Replace ("*", "(.*)"));
		}

		void MatchType (TypeDefinition type, Regex regex, XPathNavigator nav)
		{
			if (regex.Match (type.FullName).Success)
				ProcessType (type, nav);

			if (!type.HasNestedTypes)
				return;

			foreach (var nt in type.NestedTypes)
				MatchType (nt, regex, nav);
		}

		void MatchExportedType (ExportedType exportedType, ModuleDefinition module, Regex regex, XPathNavigator nav)
		{
			if (regex.Match (exportedType.FullName).Success) {
				Annotations.Mark (exportedType);
				Annotations.Mark (module);
				TypeDefinition type = null;
				try {
					type = exportedType.Resolve ();
				}
				catch (AssemblyResolutionException) {
				}
				if (type != null) {
					ProcessType (type, nav);
				}
			}
		}


		void ProcessTypePattern (string fullname, AssemblyDefinition assembly, XPathNavigator nav)
		{
			Regex regex = CreateRegexFromPattern (fullname);

			foreach (TypeDefinition type in assembly.MainModule.Types) {
				MatchType (type, regex, nav);
			}

			if (assembly.MainModule.HasExportedTypes) {
				foreach (var exported in assembly.MainModule.ExportedTypes) {
					MatchExportedType (exported, assembly.MainModule, regex, nav);
				}
			}
		}

		void ProcessType (TypeDefinition type, XPathNavigator nav)
		{
			TypePreserve preserve = GetTypePreserve (nav);

			if (!IsRequired (nav)) {
				Annotations.SetPreserve (type, preserve);
				return;
			}

			Annotations.Mark (type);

			if (type.IsNested) {
				var parent = type;
				while (parent.IsNested) {
					parent = parent.DeclaringType;
					Annotations.Mark (parent);
				}
			}

			if (preserve != TypePreserve.Nothing)
				Annotations.SetPreserve (type, preserve);

			if (nav.HasChildren) {
				MarkSelectedFields (nav, type);
				MarkSelectedMethods (nav, type);
				MarkSelectedEvents (nav, type);
				MarkSelectedProperties (nav, type);
			}
		}

		void MarkSelectedFields (XPathNavigator nav, TypeDefinition type)
		{
			XPathNodeIterator fields = nav.SelectChildren ("field", _ns);
			if (fields.Count == 0)
				return;

			ProcessFields (type, fields);
		}

		void MarkSelectedMethods (XPathNavigator nav, TypeDefinition type)
		{
			XPathNodeIterator methods = nav.SelectChildren ("method", _ns);
			if (methods.Count == 0)
				return;

			ProcessMethods (type, methods);
		}

		void MarkSelectedEvents (XPathNavigator nav, TypeDefinition type)
		{
			XPathNodeIterator events = nav.SelectChildren ("event", _ns);
			if (events.Count == 0)
				return;

			ProcessEvents (type, events);
		}

		void MarkSelectedProperties (XPathNavigator nav, TypeDefinition type)
		{
			XPathNodeIterator properties = nav.SelectChildren ("property", _ns);
			if (properties.Count == 0)
				return;

			ProcessProperties (type, properties);
		}

		static TypePreserve GetTypePreserve (XPathNavigator nav)
		{
			string attribute = GetAttribute (nav, _preserve);
			if (attribute == null || attribute.Length == 0)
				return nav.HasChildren ? TypePreserve.Nothing : TypePreserve.All;

			try {
				return (TypePreserve) Enum.Parse (typeof (TypePreserve), attribute, true);
			} catch {
				return TypePreserve.Nothing;
			}
		}

		void ProcessFields (TypeDefinition type, XPathNodeIterator iterator)
		{
			while (iterator.MoveNext ()) {
				string value = GetSignature (iterator.Current);
				if (!String.IsNullOrEmpty (value))
					ProcessFieldSignature (type, value);

				value = GetAttribute (iterator.Current, "name");
				if (!String.IsNullOrEmpty (value))
					ProcessFieldName (type, value);
			}
		}

		void ProcessFieldSignature (TypeDefinition type, string signature)
		{
			FieldDefinition field = GetField (type, signature);
			MarkField (type, field, signature);
		}

		void MarkField (TypeDefinition type, FieldDefinition field, string signature)
		{
			if (field != null)
				Annotations.Mark (field);
			else
				AddUnresolveMarker (string.Format ("T: {0}; F: {1}", type, signature));
		}

		void ProcessFieldName (TypeDefinition type, string name)
		{
			if (!type.HasFields)
				return;

			foreach (FieldDefinition field in type.Fields)
				if (field.Name == name)
					MarkField (type, field, name);
		}

		static FieldDefinition GetField (TypeDefinition type, string signature)
		{
			if (!type.HasFields)
				return null;

			foreach (FieldDefinition field in type.Fields)
				if (signature == GetFieldSignature (field))
					return field;

			return null;
		}

		static string GetFieldSignature (FieldDefinition field)
		{
			return field.FieldType.FullName + " " + field.Name;
		}

		void ProcessMethods (TypeDefinition type, XPathNodeIterator iterator)
		{
			while (iterator.MoveNext ()) {
				string value = GetSignature (iterator.Current);
				if (!String.IsNullOrEmpty (value))
					ProcessMethodSignature (type, value);

				value = GetAttribute (iterator.Current, "name");
				if (!String.IsNullOrEmpty (value))
					ProcessMethodName (type, value);
			}
		}

		void ProcessMethodSignature (TypeDefinition type, string signature)
		{
			MethodDefinition meth = GetMethod (type, signature);
			MarkMethod (type, meth, signature);
		}

		void MarkMethod (TypeDefinition type, MethodDefinition method, string signature)
		{
			if (method != null) {
				MarkMethod (method);
			} else
				AddUnresolveMarker (string.Format ("T: {0}; M: {1}", type, signature));
		}

		void MarkMethod (MethodDefinition method)
		{
			Annotations.Mark (method);
			Annotations.SetAction (method, MethodAction.Parse);
		}

		void MarkMethodIfNotNull (MethodDefinition method)
		{
			if (method == null)
				return;

			MarkMethod (method);
		}

		void ProcessMethodName (TypeDefinition type, string name)
		{
			if (!type.HasMethods)
				return;

			foreach (MethodDefinition method in type.Methods)
				if (name == method.Name)
					MarkMethod (type, method, name);
		}

		static MethodDefinition GetMethod (TypeDefinition type, string signature)
		{
			if (type.HasMethods)
				foreach (MethodDefinition meth in type.Methods)
					if (signature == GetMethodSignature (meth))
						return meth;

			return null;
		}

		static string GetMethodSignature (MethodDefinition meth)
		{
			StringBuilder sb = new StringBuilder ();
			sb.Append (meth.ReturnType.FullName);
			sb.Append (" ");
			sb.Append (meth.Name);
			sb.Append ("(");
			if (meth.HasParameters) {
				for (int i = 0; i < meth.Parameters.Count; i++) {
					if (i > 0)
						sb.Append (",");

					sb.Append (meth.Parameters [i].ParameterType.FullName);
				}
			}
			sb.Append (")");
			return sb.ToString ();
		}

		void ProcessEvents (TypeDefinition type, XPathNodeIterator iterator)
		{
			while (iterator.MoveNext ()) {
				string value = GetSignature (iterator.Current);
				if (!String.IsNullOrEmpty (value))
					ProcessEventSignature (type, value);

				value = GetAttribute (iterator.Current, "name");
				if (!String.IsNullOrEmpty (value))
					ProcessEventName (type, value);
			}
		}

		void ProcessEventSignature (TypeDefinition type, string signature)
		{
			EventDefinition @event = GetEvent (type, signature);
			MarkEvent (type, @event, signature);
		}

		void MarkEvent (TypeDefinition type, EventDefinition @event, string signature)
		{
			if (@event != null) {
				Annotations.Mark (@event);

				MarkMethod (@event.AddMethod);
				MarkMethod (@event.RemoveMethod);
				MarkMethodIfNotNull (@event.InvokeMethod);
			} else
				AddUnresolveMarker (string.Format ("T: {0}; E: {1}", type, signature));
		}

		void ProcessEventName (TypeDefinition type, string name)
		{
			if (!type.HasEvents)
				return;

			foreach (EventDefinition @event in type.Events)
				if (@event.Name == name)
					MarkEvent (type, @event, name);
		}

		static EventDefinition GetEvent (TypeDefinition type, string signature)
		{
			if (!type.HasEvents)
				return null;

			foreach (EventDefinition @event in type.Events)
				if (signature == GetEventSignature (@event))
					return @event;

			return null;
		}

		static string GetEventSignature (EventDefinition @event)
		{
			return @event.EventType.FullName + " " + @event.Name;
		}

		void ProcessProperties (TypeDefinition type, XPathNodeIterator iterator)
		{
			while (iterator.MoveNext ()) {
				string value = GetSignature (iterator.Current);
				if (!String.IsNullOrEmpty (value))
					ProcessPropertySignature (type, value, GetAccessors (iterator.Current));

				value = GetAttribute (iterator.Current, "name");
				if (!String.IsNullOrEmpty (value))
					ProcessPropertyName (type, value, _accessorsAll);
			}
		}

		void ProcessPropertySignature (TypeDefinition type, string signature, string[] accessors)
		{
			PropertyDefinition property = GetProperty (type, signature);
			MarkProperty (type, property, signature, accessors);
		}

		void MarkProperty (TypeDefinition type, PropertyDefinition property, string signature, string[] accessors)
		{
			if (property != null) {
				Annotations.Mark (property);

				MarkPropertyAccessors (type, property, accessors);
			} else
				AddUnresolveMarker (string.Format ("T: {0}; P: {1}", type, signature));
		}

		void MarkPropertyAccessors (TypeDefinition type, PropertyDefinition property, string[] accessors)
		{
			if (Array.IndexOf (accessors, "all") >= 0) {
				MarkMethodIfNotNull (property.GetMethod);
				MarkMethodIfNotNull (property.SetMethod);

				return;
			}
			if (property.GetMethod != null 
					&& Array.IndexOf (accessors, "get") >= 0)
				MarkMethod (property.GetMethod);
			else if (property.GetMethod == null)
				AddUnresolveMarker (string.Format ("T: {0}' M: {1} get_{2}", type, property.PropertyType, property.Name));
			
			if (property.SetMethod != null 
					&& Array.IndexOf (accessors, "set") >= 0)
				MarkMethod (property.SetMethod);
			else if (property.SetMethod == null)
				AddUnresolveMarker (string.Format ("T: {0}' M: System.Void set_{2} ({1})", type, property.PropertyType, property.Name));
		}

		void ProcessPropertyName (TypeDefinition type, string name, string[] accessors)
		{
			if (!type.HasProperties)
				return;

			foreach (PropertyDefinition property in type.Properties)
				if (property.Name == name)
					MarkProperty (type, property, name, accessors);
		}

		static PropertyDefinition GetProperty (TypeDefinition type, string signature)
		{
			if (!type.HasProperties)
				return null;

			foreach (PropertyDefinition property in type.Properties)
				if (signature == GetPropertySignature (property))
					return property;

			return null;
		}

		static string GetPropertySignature (PropertyDefinition property)
		{
			return property.PropertyType.FullName + " " + property.Name;
		}

		static AssemblyDefinition GetAssembly (LinkContext context, string assemblyName)
		{
			AssemblyNameReference reference = AssemblyNameReference.Parse (assemblyName);
			AssemblyDefinition assembly;

			assembly = context.Resolve (reference);

			ProcessReferences (assembly, context);
			return assembly;
		}

		static void ProcessReferences (AssemblyDefinition assembly, LinkContext context)
		{
			context.ResolveReferences (assembly);
		}

		static bool IsRequired (XPathNavigator nav)
		{
			string attribute = GetAttribute (nav, _required);
			if (attribute == null || attribute.Length == 0)
				return true;

			return TryParseBool (attribute);
		}

		static bool TryParseBool (string s)
		{
			try {
				return bool.Parse (s);
			} catch {
				return false;
			}
		}

		static string GetSignature (XPathNavigator nav)
		{
			return GetAttribute (nav, _signature);
		}

		static string GetFullName (XPathNavigator nav)
		{
			return GetAttribute (nav, _fullname);
		}

		static string[] GetAccessors (XPathNavigator nav)
		{
			string accessorsValue = GetAttribute (nav, _accessors);

			if (accessorsValue != null)	{
				string[] accessors = accessorsValue.Split (
					_accessorsSep, StringSplitOptions.RemoveEmptyEntries);

				if (accessors.Length > 0) {
					for (int i = 0; i < accessors.Length; ++i)
						accessors[i] = accessors[i].ToLower ();

					return accessors;
				}
			}
			return _accessorsAll;
		}

		static string GetAttribute (XPathNavigator nav, string attribute)
		{
			return nav.GetAttribute (attribute, _ns);
		}

		public override string ToString ()
		{
			return "ResolveFromXmlStep: " + _xmlDocumentLocation;
		}
	}
}
