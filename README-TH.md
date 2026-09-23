# NetStuck

NetStuck เป็นโปรแกรม Network diagnostics และ Config Collector แบบ Portable สำหรับ Windows/.NET Framework 4.x รุ่น **v1.3.2** จัดทำเพื่อให้เจ้าของลองกด Update now จาก 1.3.1 โดยคงฟังก์ชันเดิมและข้าม tests ตามคำสั่ง ผลทดสอบ 1.3.1 เดิมไม่ใช่ผลทดสอบแพ็กเกจนี้

## ความสามารถหลัก

- Live Ping: ICMP/TCP, รองรับ CIDR, Profile, History, Filter และปรับ Column ได้
- Traceroute: ทำงานพร้อมกัน 2 Session, แสดง latency/loss/jitter, Route/DNS event, Hop Description และ ISP/ASN
- DNS Resolver: Forward/Reverse DNS, Query latency และ Polling ต่อเนื่อง
- MAC / WAN Lookup: ตรวจ MAC Vendor และข้อมูลเจ้าของ Public IP
- Calculators: IP/CIDR และแปลงหน่วย Network
- Config Collector: SSH/Telnet พร้อม AUTH1/AUTH2 fallback, เก็บ TXT/JSON แบบ streaming และ export error CSV

## Baseline และ candidate

คำสั่งมาตรฐานสำหรับการตรวจสอบรุ่นปกติอยู่ด้านล่าง แต่ไม่ได้รันสำหรับรุ่นทดลองอัปเดต 1.3.2 นี้:

```powershell
.\scripts\Test-NetStuck.ps1 -SoakSeconds 10
```

Build อย่างเดียว:

```powershell
.\build_windows.bat
```

ไฟล์ที่ Build จะอยู่ใน `artifacts\build\NetStuck.exe` และจะไม่ถูกเก็บใน Git history

## เอกสารสำหรับดูแลโปรเจกต์

- [AGENTS.md](AGENTS.md) — ข้อกำหนดสำหรับ AI และผู้แก้โค้ด
- [Architecture](docs/ARCHITECTURE.md) — หน้าที่ของแต่ละ source file และ data flow
- [Development](docs/DEVELOPMENT.md) — วิธีเตรียมเครื่องและแก้ไขโค้ด
- [Testing](docs/TESTING.md) — ชุดทดสอบและ acceptance gate
- [Privacy](PRIVACY.md) — ข้อมูลที่โปรแกรมเก็บและบริการภายนอก
- [Releasing](docs/RELEASING.md) — ขั้นตอนออกเวอร์ชัน

ข้อมูล Runtime อยู่ที่ `%LOCALAPPDATA%\NetStuck` และ Config Collector ใช้ `%USERPROFILE%\Documents\NetStuck Configs` เป็นค่าเริ่มต้น ห้ามนำไฟล์จากสองตำแหน่งนี้ขึ้น GitHub เพราะอาจมี IP, Username, Network topology และ Device configuration

สำหรับนำไปใช้งาน ให้ดาวน์โหลด ZIP ทั้งชุดจาก GitHub Releases และเก็บ `NetStuck.exe`, โฟลเดอร์ `tools` และ PuTTY license ไว้ด้วยกัน
