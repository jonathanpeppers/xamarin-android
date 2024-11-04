package com.microsoft.android;

public class Foo {
    static final Foo shared = new Foo();

    public static Foo sharedFoo() {
        return shared;
    }

    public static Foo newFoo() {
        return new Foo();
    }
}