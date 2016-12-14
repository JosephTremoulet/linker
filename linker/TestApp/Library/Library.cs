using System;

public class Library
{
    private int _myint;
    private int _unused;

    public Library()
    {
        _myint = 1;
    }

    // unused
    public Library(int myint)
    {
        _myint = myint;
    }
    
    public int Hello()
    {
        return _myint;
    }

    public void Unused(int unused)
    {
        _unused = unused;
    }
}

class Unused {
}

