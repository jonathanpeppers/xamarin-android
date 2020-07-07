package com.xamarin.signflinger;

import java.io.ByteArrayInputStream;
import java.io.File;
import java.io.IOException;
import java.nio.file.Files;
import java.security.KeyFactory;
import java.security.NoSuchAlgorithmException;
import java.security.PrivateKey;
import java.security.cert.CertificateException;
import java.security.cert.CertificateFactory;
import java.security.cert.X509Certificate;
import java.security.spec.InvalidKeySpecException;
import java.security.spec.PKCS8EncodedKeySpec;
import java.util.ArrayList;
import java.util.Collection;
import java.util.InvalidPropertiesFormatException;
import java.util.List;

import com.android.signflinger.SignedApk;
import com.android.signflinger.SignedApkOptions;
import com.android.zipflinger.BytesSource;

public class Main {
    public static void main(String[] args) {
        if (args.length == 0) {
            System.out.println("Pass at least one file!");
            return;
        }

        try {
            File apkFile = new File("foo.apk");
            long apkLastWrite = apkFile.lastModified();

            SignedApkOptions options = new SignedApkOptions.Builder()
                .setV2Enabled(true)
                .setPrivateKey(getPrivateKey())
                .setCertificates(getCertificateChain())
                .setMinSdkVersion(24)
                .build();
            SignedApk archive = new SignedApk(apkFile, options);

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
        } catch (Exception ex) {
            throw new RuntimeException(ex);
        }
    }

    private static List<X509Certificate> getCertificateChain() throws IOException, CertificateException {
        byte[] bytes = Files.readAllBytes(new File ("rsa-1024.x509.pem").toPath());
        CertificateFactory certificateFactory = CertificateFactory.getInstance("X.509");
        Collection<? extends java.security.cert.Certificate> certs = certificateFactory.generateCertificates(new ByteArrayInputStream(bytes));
        List<X509Certificate> result = new ArrayList<>(certs.size());
        for (java.security.cert.Certificate cert : certs) {
            result.add((X509Certificate) cert);
        }
        return result;
    }

    private static PrivateKey getPrivateKey()
            throws IOException, InvalidPropertiesFormatException, NoSuchAlgorithmException, InvalidKeySpecException {
        byte[] bytes = Files.readAllBytes(new File ("rsa-1024.pk8").toPath());
        KeyFactory keyFactory = KeyFactory.getInstance("rsa");
        return keyFactory.generatePrivate(new PKCS8EncodedKeySpec(bytes));
    }
}
