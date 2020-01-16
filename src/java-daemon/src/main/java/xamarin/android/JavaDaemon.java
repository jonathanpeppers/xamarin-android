package xamarin.android;

import org.apache.tools.ant.Project;
import org.apache.tools.ant.taskdefs.Java;
import org.apache.tools.ant.types.Commandline.Argument;
import org.apache.tools.ant.types.Path;
import org.w3c.dom.*;
import org.xml.sax.InputSource;

import javax.xml.parsers.*;
import javax.xml.transform.*;
import javax.xml.transform.dom.DOMSource;
import javax.xml.transform.stream.StreamResult;
import java.io.*;
import java.util.Scanner;

public class JavaDaemon {
    static DocumentBuilder builder;
    static Transformer transformer;

    public static void main (String[] args)
            throws ParserConfigurationException, TransformerException, IOException {
        // Examples
        // <Java ClassName="com.android.tools.r8.D8" Jar="C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Xamarin\Android\r8.jar" Arguments="--version" />
        // <Java ClassName="com.android.tools.r8.D8" Jar="C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\MSBuild\Xamarin\Android\r8.jar" Arguments="--version" />
        // <Java ClassName="com.android.tools.r8.R8" Jar="C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Xamarin\Android\r8.jar" Arguments="--version" />
        // <Java ClassName="com.android.tools.r8.R8" Jar="C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\MSBuild\Xamarin\Android\r8.jar" Arguments="--version" />
        // <Java Exit="True" />
        Scanner scanner = new Scanner(System.in);
        DocumentBuilderFactory factory = DocumentBuilderFactory.newInstance();
        builder = factory.newDocumentBuilder();
        TransformerFactory transformerFactory = TransformerFactory.newInstance();
        transformer = transformerFactory.newTransformer();
        transformer.setOutputProperty(OutputKeys.OMIT_XML_DECLARATION, "yes");
        while (true) {
            String line = scanner.nextLine(); //NOTE: that this line exits the process if the parent process dies
            StringReader reader = new StringReader(line);
            try {
                Document document = builder.parse(new InputSource(reader));
                Element input = document.getDocumentElement();
                if (!input.getAttribute("Exit").isEmpty()) {
                    break;
                }
                exec(input);
            } catch (Exception e) {
                out (-1, "", toErrorString (e));
            } finally {
                reader.close();
            }
            // Try to free as much memory as we can while idle
            System.gc();
        }
    }

    static void exec (Element input)
            throws IOException, TransformerException {
        PrintStream oldSystemOut = System.out;
        PrintStream oldSystemErr = System.err;
        try (ByteArrayOutputStream outStream = new ByteArrayOutputStream();
             ByteArrayOutputStream errStream = new ByteArrayOutputStream();
             PrintStream outPrintStream = new PrintStream(outStream, true);
             PrintStream errPrintStream = new PrintStream(errStream, true)) {
            System.setOut(outPrintStream);
            System.setErr(errPrintStream);
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
            out(exitCode, outStream.toString(), errStream.toString());
        }
    }

    static void out (int exitCode, String out, String err)
            throws TransformerException {
        Document document = builder.newDocument();
        Element java = document.createElement("Java");
        java.setAttribute("ExitCode", Integer.toString(exitCode));
        if (!out.isEmpty())
            java.setAttribute("StandardOutput", out);
        if (!err.isEmpty())
            java.setAttribute("StandardError", err);
        document.appendChild(java);
        transformer.transform(new DOMSource(document), new StreamResult(System.out));
        System.out.println();
    }

    static String toErrorString (Throwable t)
            throws IOException {
        StringWriter sw = new StringWriter();
        PrintWriter pw = new PrintWriter(sw);
        try {
            t.printStackTrace(pw);
        } finally {
            pw.close();
            sw.close();
        }
        return sw.toString();
    }
}
