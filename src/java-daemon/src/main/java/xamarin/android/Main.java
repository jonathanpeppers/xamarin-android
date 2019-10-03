package xamarin.android;

import org.apache.tools.ant.Project;
import org.apache.tools.ant.taskdefs.Java;
import org.apache.tools.ant.types.Commandline.Argument;
import org.apache.tools.ant.types.Path;
import org.json.JSONObject;

import java.io.*;

public class Main {
    public static void main (String[] args) throws IOException {
        // Examples
        // { "className": "com.android.tools.r8.D8", "jar": "C:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\Enterprise\\MSBuild\\Xamarin\\Android\\r8.jar", "arguments": "--version" }
        // { "className": "com.android.tools.r8.D8", "jar": "C:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\Preview\\MSBuild\\Xamarin\\Android\\r8.jar", "arguments": "--version" }
        // { "className": "com.android.tools.r8.R8", "jar": "C:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\Enterprise\\MSBuild\\Xamarin\\Android\\r8.jar", "arguments": "--version" }
        // { "className": "com.android.tools.r8.R8", "jar": "C:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\Preview\\MSBuild\\Xamarin\\Android\\r8.jar", "arguments": "--version" }

        while (true) {
            BufferedReader reader = new BufferedReader(new InputStreamReader(System.in));
            String line = reader.readLine();
            try {
                JSONObject input = new JSONObject(line);
                if (input.has("exit")) {
                    break;
                }
                exec(input);
            } catch (Exception e) {
                out (-1, "", e.getMessage());
            }

            // Try to free as much memory as we can while idle
            System.gc();
        }
    }

    static void exec (JSONObject input) throws IOException {
        String oldWorkingDirectory = null;
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
                java.setClassname(input.getString("className"));
                Argument arg = java.getCommandLine().createArgument();
                arg.setLine(input.getString("arguments"));

                Path path = java.createClasspath();
                path.setPath(input.getString("jar"));
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
    {
        JSONObject json = new JSONObject();
        json.put("exitCode", exitCode);
        json.put("stdout", out);
        json.put("stderr", err);
        System.out.println(json.toString());
    }
}
