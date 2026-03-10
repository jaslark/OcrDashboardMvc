ssh -i C:\Users\ADMIN\TrumVN.pem ubuntu@18.143.165.239

sudo apt update

CPU + RAM: hstop

Disk: df -h
------------

#Test PostgreSQL:
psql -h localhost -U postgres -d ocrmvcdatabase

#Test connection
psql "host=localhost user=postgres password=123456 dbname=ocrmvcdatabase"


#Clone project từ GitHub
git clone https://github.com/<your-repo>.git
cd <your-repo>

#Kiểm tra .sln
ls

#Restore package
dotnet restore

#Build project
dotnet build

#Publish để chạy production:
dotnet publish -c Release -o publish
cd publish

#Tắt terminal - mất session
dotnet OcrDashboardMvc.dll --urls "http://0.0.0.0:5000" (http://18.143.165.239:5000)
#Nên dùng:
nohup dotnet OcrDashboardMvc.dll --urls "http://0.0.0.0:5000" &

#Sửa config
nano publish/appsettings.json