package xamarin.android;

import org.apache.tools.ant.Project;
import org.apache.tools.ant.taskdefs.Java;
import org.apache.tools.ant.types.Commandline.Argument;
import org.apache.tools.ant.types.Path;
import org.w3c.dom.*;
import org.xml.sax.InputSource;

import javax.sound.sampled.Line;
import javax.xml.parsers.*;
import javax.xml.transform.*;
import javax.xml.transform.dom.DOMSource;
import javax.xml.transform.stream.StreamResult;
import java.io.*;
import java.util.*;

// Examples of input:
// <Java ClassName="com.android.tools.r8.D8" Jar="C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Xamarin\Android\r8.jar" Arguments="--version" />
// <Java ClassName="com.android.tools.r8.D8" Jar="C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\MSBuild\Xamarin\Android\r8.jar" Arguments="--version" />
// <Java ClassName="com.android.tools.r8.R8" Jar="C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Xamarin\Android\r8.jar" Arguments="--version" />
// <Java ClassName="com.android.tools.r8.R8" Jar="C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\MSBuild\Xamarin\Android\r8.jar" Arguments="--version" />
// <Java Exit="True" />

public class JavaDaemon {
    private static DocumentBuilder builder;
    private static Transformer transformer;

    public static void main (String[] args)
            throws ParserConfigurationException, TransformerException, IOException {
        Scanner scanner = new Scanner(System.in);
        DocumentBuilderFactory factory = DocumentBuilderFactory.newInstance();
        builder = factory.newDocumentBuilder();
        TransformerFactory transformerFactory = TransformerFactory.newInstance();
        transformer = transformerFactory.newTransformer();
        transformer.setOutputProperty(OutputKeys.OMIT_XML_DECLARATION, "yes");
        while (true) {
            StringReader reader = null;
            try {
                //This line throws NoSuchElementException if the parent process dies
                String line = scanner.nextLine();
                reader = new StringReader(line);
                Document document = builder.parse(new InputSource(reader));
                Element input = document.getDocumentElement();
                if (!input.getAttribute("Exit").isEmpty()) {
                    break;
                }
                exec(input);
            } catch (NoSuchElementException e) {
                //This means that scanner.nextLine() reached the end, we can exit
                break;
            } catch (Exception e) {
                out (-1, new String[0], toErrorString (e));
            } finally {
                if (reader != null)
                    reader.close();
            }
            // Try to free as much memory as we can while idle
            System.gc();
        }
        scanner.close();
    }

    static void exec (Element input)
            throws IOException, TransformerException {
        PrintStream oldSystemOut = System.out;
        PrintStream oldSystemErr = System.err;
        try (LineBuffer outBuffer = new LineBuffer();
             LineBuffer errBuffer = new LineBuffer()) {
            System.setOut(outBuffer);
            System.setErr(errBuffer);
            int exitCode = 0;
            try {
                Java java = new Java();
                java.setProject(new Project());
                java.setClassname(input.getAttribute("ClassName"));
                Argument arg = java.getCommandLine().createArgument();
                arg.setLine(input.getAttribute("Arguments"));

                Path path = java.createClasspath();
                path.setPath(input.getAttribute("Jar"));
                java.setClasspath(path);

                exitCode = java.executeJava();
            } finally {
                System.setOut(oldSystemOut);
                System.setErr(oldSystemErr);
            }
            // NOTE: we have to call out() *after* System.out/err is restored
            out(exitCode, outBuffer.getLines(), errBuffer.getLines());
        }
    }

    static void out (int exitCode, String[] out, String[] err)
            throws TransformerException {
        Document document = builder.newDocument();
        Element java = document.createElement("Java");
        java.setAttribute("ExitCode", Integer.toString(exitCode));
        for (String line : out) {
            Element child = document.createElement("StandardOutput");
            child.setTextContent(line);
            java.appendChild(child);
        }
        for (String line : err) {
            Element child = document.createElement("StandardError");
            child.setTextContent(line);
            java.appendChild(child);
        }
        document.appendChild(java);
        transformer.transform(new DOMSource(document), new StreamResult(System.out));
        System.out.println();
    }

    static String[] toErrorString (Throwable t)
            throws IOException {
        try (LineBuffer buffer = new LineBuffer()) {
            t.printStackTrace(buffer);
            buffer.flush();
            return buffer.getLines();
        }
    }
}
