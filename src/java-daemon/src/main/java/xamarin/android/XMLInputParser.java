package xamarin.android;

import org.xml.sax.Attributes;
import org.xml.sax.SAXException;
import org.xml.sax.helpers.DefaultHandler;

public class XMLInputParser extends DefaultHandler {
    Attributes attributes;

    public Attributes getAttributes () {
        return attributes;
    }

    @Override
    public void startElement(String uri, String localName, String qName, Attributes attributes) throws SAXException {
        if (localName == "command") {
            this.attributes = attributes;
        }
    }
}
