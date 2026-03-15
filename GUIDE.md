
Cách sửa chuẩn trên Windows (PowerShell)

Chạy lần lượt:

icacls C:\Users\ADMIN\TrumVN.pem /inheritance:r
icacls C:\Users\ADMIN\TrumVN.pem /remove "CodexSandboxUsers"
icacls C:\Users\ADMIN\TrumVN.pem /grant:r ADMIN:R

Sau đó kiểm tra:

icacls C:\Users\ADMIN\TrumVN.pem

Kết quả chỉ nên có kiểu như:

ADMIN:(R)
Sau đó SSH lại
ssh -i C:\Users\ADMIN\TrumVN.pem ubuntu@18.143.165.239   -   ssh -i C:\Users\ADMIN\TrumVN.pem ubuntu@47.130.48.97

**#Access server**

`ssh -i C:\Users\ADMIN\TrumVN.pem ubuntu@18.143.165.239`

`sudo apt update`

**#CPU + RAM:** 

`hstop`

**#Disk:**

`df -h`


------------

**#Test PostgreSQL:**

`psql -h localhost -U postgres -d ocrmvcdatabase`

**#Test connection**

`psql "host=localhost user=postgres password=123456 dbname=ocrmvcdatabase"`


**#Clone project từ GitHub**

`git clone https://github.com/jaslark/OcrDashboardMvc.git`

`cd <your-repo>`

**#Kiểm tra .sln**

`ls`

**#Restore package**

`dotnet restore`

**#Build project**

`dotnet build`

**#Publish để chạy production:**

`dotnet publish OcrDashboardMvc.csproj -c Release -o publish`

`cd publish`

**Kill process

`sudo fuser -k 5000/tcp`

**#Tắt terminal - mất session**

`dotnet OcrDashboardMvc.dll --urls "http://0.0.0.0:5000" (http://18.143.165.239:5000)`

**#Nên dùng - giữ session:**

`nohup dotnet OcrDashboardMvc.dll --urls "http://0.0.0.0:5000" &`

**#Sửa config**

`nano publish/appsettings.json`

kiểm tra port đã chạy được chưa:

`ss -tulnp`

Cách sửa tốt nhất (khuyên dùng)

Build self-contained để không cần runtime trên server.

Trên máy build (hoặc ngay trên server):

dotnet publish -c Release -r linux-arm64 --self-contained true -o publish

Giải thích:

Option	Meaning
-r linux-arm64	server bạn là ARM64
--self-contained true	đóng gói runtime vào app
-c Release	build production

Sau khi publish xong, trong folder publish sẽ có binary executable.

Chạy trực tiếp:

`nohup ./OcrDashboardMvc --urls=http://0.0.0.0:5555 > app.log 2>&1 &`

Không cần dotnet.