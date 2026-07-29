import 'package:flutter/material.dart';
import 'screens/home_screen.dart';

void main() {
  runApp(const RemoteDesktopApp());
}

class RemoteDesktopApp extends StatelessWidget {
  const RemoteDesktopApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'التحكم عن بعد',
      debugShowCheckedModeBanner: false,
      // نتحكم بالاتجاه (RTL) يدوياً عبر Directionality تحت - ما نحتاج حزمة intl لهذا المشروع البسيط
      builder: (context, child) {
        return Directionality(
          textDirection: TextDirection.rtl,
          child: child!,
        );
      },
      theme: ThemeData(
        useMaterial3: true,
        colorSchemeSeed: Colors.indigo,
        brightness: Brightness.dark,
        fontFamily: 'Cairo', // أضف خط عربي مثل Cairo أو Tajawal في مجلد fonts لاحقاً
      ),
      home: const HomeScreen(),
    );
  }
}
