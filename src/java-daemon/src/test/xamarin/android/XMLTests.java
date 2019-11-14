package xamarin.android;

import org.junit.Assert;
import org.junit.Test;
import org.xml.sax.InputSource;
import org.xml.sax.SAXException;

import javax.xml.parsers.ParserConfigurationException;
import javax.xml.parsers.SAXParser;
import javax.xml.parsers.SAXParserFactory;
import java.io.IOException;
import java.io.StringReader;

public class XMLTests {
    @Test
    public void parseXml () throws ParserConfigurationException, SAXException, IOException {
        SAXParserFactory factory = SAXParserFactory.newInstance();
        SAXParser parser = factory.newSAXParser();
        InputSource source = new InputSource(new StringReader("<command foo=\"bar\" />"));
        XMLInputParser handler = new XMLInputParser();
        parser.parse(source, handler);

        Assert.assertEquals("bar", handler.getAttributes().getValue("foo"));
    }
}
