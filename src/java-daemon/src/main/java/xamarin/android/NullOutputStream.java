package xamarin.android;

import java.io.OutputStream;

class NullOutputStream extends OutputStream {
    @Override
    public void write(int b) { }
}
