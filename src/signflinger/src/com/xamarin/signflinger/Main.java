package com.xamarin.signflinger;

import java.io.File;
import java.nio.file.Files;
import java.util.ArrayList;
import java.util.Map;

import com.android.signflinger.SignedApk;
import com.android.signflinger.SignedApkOptions;
import com.android.zipflinger.BytesSource;
import com.android.zipflinger.Entry;
import com.android.zipflinger.ZipArchive;

public class Main {
    public static void main(String[] args) {
        try {
            File apkFile = new File("com.companyname.signflinger.apk");
            //long apkLastWrite = apkFile.lastModified();

            byte[] certBytes = Files.readAllBytes(new File ("rsa-1024.x509.pem").toPath());
            byte[] keyBytes = Files.readAllBytes(new File ("rsa-1024.pk8").toPath());

            SignedApkOptions options = new SignedApkOptions.Builder()
                .setV1Enabled(true)
                .setCertificates(SignedApkOptions.bytesToCertificateChain(certBytes))
                .setPrivateKey(SignedApkOptions.bytesToPrivateKey("rsa", keyBytes))
                .setMinSdkVersion(24)
                .build();
            SignedApk archive = new SignedApk(apkFile, options);
            Map<String, Entry> entries = apkFile.exists() ? ZipArchive.listEntries(apkFile) : null;

            ArrayList<File> files = new ArrayList<File>();
            for (String path : args) {
                File file = new File(path);
                if (entries == null || !entries.containsKey("assemblies/" + path)) {
                    System.out.println("New File: " + path);
                    files.add(file);
                } else {
                    System.out.println("Deleting: " + path);
                    archive.delete("assemblies/" + path);
                    files.add(file);
                }
            }
            for (File file : files) {
                String name = file.getName();
                System.out.println("Adding: " + name);
                archive.add(new BytesSource(file, "assemblies/" + name, 0));
            }
            archive.close();
        } catch (Exception ex) {
            throw new RuntimeException(ex);
        }
    }
}
