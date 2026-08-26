NetStuck v.1.2.3
=================

โปรแกรม Network Toolbox แบบ Portable สำหรับ Windows เปิด NetStuck.exe ใช้งานได้ทันที
ไม่ต้องติดตั้ง Python และต้องเก็บ NetStuck.exe กับโฟลเดอร์ tools ไว้ด้วยกัน

รายการแก้ไข v.1.2.3
--------------------

- จัดช่อง Traceroute เป็น Grid ที่มีคอลัมน์แน่นอน ช่อง Input จึงเรียงต่อกันและไม่ซ้อนกัน
- แถวบนเป็น Target, Max Hops, Timeout และ Interval
- แถวล่างเป็น Protocol, Port และ Packet Size ทำให้รายการ Protocol ไม่เปิดทับช่อง Timing

รายการเดิมจาก v.1.2.2
----------------------

- ย้าย Start, Pause และ Stop ไปแถวแยกภายในกรอบ Traceroute ทำให้ปุ่มไม่สามารถ
  ล้นมาทับช่อง Max Hops, Timeout หรือ Interval เมื่อหน้าต่างแคบหรือใช้ DPI สูง
- แก้ SplitContainer ที่ความกว้างขั้นต่ำ 1100 px ให้ Result และ Event Log
  รักษาขนาดขั้นต่ำที่กำหนดไว้
- ทำพื้นหลัง ComboBox และ NumericUpDown ให้ใช้สีเดียวกันทั้ง Enabled/Disabled
- เปลี่ยนชื่อ Port เป็น Port (TCP/UDP) โดย ICMP จะไม่ใช้ค่าช่องนี้

รายการเดิมจาก v.1.2.1
----------------------

- จัดส่วนตั้งค่า Traceroute ใหม่เป็น control panel แบบมีกรอบและมีสองแถว
- Target, Protocol, Port และ Packet Size อยู่แถวบนในแนวเดียวกัน
- Max Hops, Timeout และ Interval อยู่ด้านซ้ายของแถวล่าง ส่วน Start, Pause และ
  Stop อยู่ด้านขวาและกว้างเท่ากัน
- ลดความสูงส่วนตั้งค่าลง 38 px ทำให้มีพื้นที่ตารางผลลัพธ์มากขึ้น
- ไม่เปลี่ยน polling, adaptive TTL หรือ logic ของ Config Collector จาก v.1.2.0

รายการเดิมจาก v.1.2.0
----------------------

- Config Collector export เฉพาะรายการที่ error เป็น CSV โดยมีคอลัมน์ IP,
  Status, Protocol, Username และ Detail
- Terminal ของ Collector รวมข้อความเป็น batch ก่อนวาด UI รองรับการเก็บพร้อมกัน
  16-32 เครื่องได้ลื่นขึ้น
- SSH/Telnet เขียน config ลง temporary file ระหว่างรับข้อมูลจริง ไม่เก็บ config
  ขนาดใหญ่ทั้งหมดไว้ใน RAM และสร้าง TXT/JSON หลังเก็บสำเร็จ
- Traceroute อัปเดตเฉพาะ hop ที่เปลี่ยน ไม่ ResetBindings ทั้งตาราง จึงไม่ดึง
  scroll หรือ current cell กลับระหว่าง polling
- เพิ่ม adaptive TTL polling: ปลายทางถูกตรวจบ่อย, hop ที่นิ่งหมุนเวียนตรวจ,
  มี full discovery เป็นระยะ และกลับไปตรวจเต็ม 3 รอบทันทีเมื่อ route เปลี่ยน
- ISP/ASN cache และ reverse-DNS cache เก็บข้ามการเปิดโปรแกรมพร้อม TTL
- ปรับ Protocol, Port, Packet Size, Max Hops, Timeout และ Interval ให้กว้างเท่ากัน
  และมีกรอบครบ
- ลบ source เดิมของ Log Sanitizer หลังผ่าน compatibility release แล้ว
- เพิ่ม overnight soak test สำหรับ packet loss, route/DNS change, TACACS rejection,
  VTY limit, terminal load, memory และ UI responsiveness

หมายเหตุ Config Collector
-------------------------

- DOMAIN\username ถูกส่งเป็น backslash จริงหนึ่งตัว และข้อความที่วางมาเป็นสองตัว
  จะถูกปรับเป็นหนึ่งตัว
- AUTH2 ทำต่อจาก AUTH1 เมื่อเป็น authentication rejection เท่านั้น
- Password และ enable secret ไม่ถูกบันทึกใน state, CSV หรือ process argument
- Config ฉบับเต็มอยู่ใน TXT ที่บันทึก ส่วน terminal จะแสดง preview แบบจำกัดขนาด
  เมื่อ output มีขนาดใหญ่มาก

การนำไปแจก
-----------

คัดลอกโฟลเดอร์ NetStuck-v.1.2.3 ไปทั้งโฟลเดอร์ โดยเก็บ NetStuck.exe, tools,
license และไฟล์ประกอบไว้ด้วยกัน ข้อมูลผู้ใช้และ cache เก็บที่:

  %LOCALAPPDATA%\NetStuck

ดูผลทดสอบที่ TEST-REPORT.txt และรายละเอียด performance/แผนรุ่นถัดไปที่
PERFORMANCE-REPORT-AND-NEXT-PLAN.txt
