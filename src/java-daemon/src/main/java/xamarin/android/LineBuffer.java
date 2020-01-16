package xamarin.android;

import java.io.*;
import java.util.ArrayList;

class LineBuffer  extends PrintStream {
    private ArrayList<String> lines = new ArrayList<>();
    private String line = "";

    public LineBuffer() {
        super(new NullOutputStream(), false);
    }

    public String[] getLines() {
        String[] buffer = new String[lines.size()];
        return lines.toArray(buffer);
    }

    private void addToLines() {
        if (!line.isEmpty()) {
            lines.add(line);
            line = "";
        }
    }

    @Override
    public void print(String s) {
        synchronized (this) {
            super.print(s);
            line += s;
        }
    }

    @Override
    public void println() {
        synchronized (this) {
            super.println();
            addToLines();
        }
    }

    @Override
    public void close() {
        synchronized (this) {
            super.close();
            addToLines();
        }
    }

    @Override
    public void flush() {
        synchronized (this) {
            super.flush();
            addToLines();
        }
    }
}
