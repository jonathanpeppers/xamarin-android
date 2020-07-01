package com.xamarin.signflinger;

import java.io.File;
import java.io.IOException;
import java.util.ArrayList;

import com.android.zipflinger.BytesSource;
import com.android.zipflinger.ZipArchive;

public class Main {
    public static void main(String[] args) throws IOException {
        if (args.length == 0) {
            System.out.println("Pass at least one file!");
            return;
        }

        File apkFile = new File ("foo.apk");
        long apkLastWrite = apkFile.lastModified();

        ZipArchive archive = new ZipArchive (apkFile);
        for (String entry : archive.listEntries()) {
            System.out.println("Existing entry: " + entry);
        }

        ArrayList<File> files = new ArrayList<File>();
        for (String path : args) {
            File file = new File(path);
            if (apkLastWrite < file.lastModified()) {
                System.out.println("Deleting: " + path);
                archive.delete(path);
                files.add(file);
            } else {
                System.out.println("Skipping: " + path);
            }
        }
        for (File file : files) {
            String name = file.getName();
            System.out.println("Adding: " + name);
            archive.add(new BytesSource(file, name, 1));
        }
        archive.close();
    }
}
