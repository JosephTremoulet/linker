using System;

class Program
{
    static void Main(string[] args)
    {
        Program p = new Program();
        Console.WriteLine("Hello returned: " + p.Run());
    }

    int Run()
    {
        Library lib = new Library();
        return lib.Hello();
    }
}
