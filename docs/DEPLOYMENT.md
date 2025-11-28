# 배포 가이드

이 문서는 192.168.0.8 서버에 ExcelLinkExtractor를 배포하고 Cloudflare Tunnel로 공개하는 방법을 설명합니다.

## 사전 준비

### 서버 요구사항
- Ubuntu 20.04+ 또는 Debian 11+ (권장)
- SSH 접근 권한
- sudo 권한
- .NET 10.0 Runtime
- Cloudflare 계정

## 1단계: 서버 준비

### 1.1 SSH 접속
```bash
ssh user@192.168.0.8
```

### 1.2 .NET 10 Runtime 설치 (Ubuntu/Debian)
```bash
# Microsoft 패키지 저장소 추가
wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# .NET 10 Runtime 설치
sudo apt-get update
sudo apt-get install -y aspnetcore-runtime-10.0

# 설치 확인
dotnet --list-runtimes
```

### 1.3 애플리케이션 디렉토리 생성
```bash
sudo mkdir -p /var/www/excellinkextractor
sudo chown $USER:$USER /var/www/excellinkextractor
```

## 2단계: 애플리케이션 빌드 및 배포

### 2.1 로컬에서 빌드 (현재 WSL 환경에서)
```bash
cd /mnt/c/dev/ExcelLinkExtractor
dotnet publish ExcelLinkExtractorWeb/ExcelLinkExtractorWeb.csproj -c Release -o ./publish
```

### 2.2 서버로 파일 전송
```bash
# 로컬(WSL)에서 실행
cd /mnt/c/dev/ExcelLinkExtractor
scp -r ./publish/* user@192.168.0.8:/var/www/excellinkextractor/
```

또는 배포 스크립트 사용:
```bash
./deploy.sh
```

## 3단계: Systemd 서비스 설정

### 3.1 서비스 파일 복사
```bash
# 서버에서 실행
sudo cp /var/www/excellinkextractor/excellinkextractor.service /etc/systemd/system/
```

### 3.2 서비스 활성화 및 시작
```bash
# systemd 리로드
sudo systemctl daemon-reload

# 서비스 활성화 (부팅 시 자동 시작)
sudo systemctl enable excellinkextractor

# 서비스 시작
sudo systemctl start excellinkextractor

# 상태 확인
sudo systemctl status excellinkextractor
```

### 3.3 로그 확인
```bash
# 실시간 로그 보기
sudo journalctl -u excellinkextractor -f

# 최근 로그 보기
sudo journalctl -u excellinkextractor -n 100
```

## 4단계: Cloudflare Tunnel 설정

### 4.1 Cloudflare Tunnel 설치 (서버에서)
```bash
# Ubuntu/Debian
wget -q https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-amd64.deb
sudo dpkg -i cloudflared-linux-amd64.deb
```

### 4.2 Cloudflare 인증
```bash
cloudflared tunnel login
```
브라우저가 열리면 Cloudflare 계정으로 로그인하고 도메인을 선택합니다.

### 4.3 Tunnel 생성
```bash
# Tunnel 생성 (이름: exceltool)
cloudflared tunnel create exceltool

# Tunnel ID 확인 (나중에 사용)
cloudflared tunnel list
```

### 4.4 Tunnel 설정 파일 생성
```bash
sudo mkdir -p /etc/cloudflared
sudo nano /etc/cloudflared/config.yml
```

다음 내용을 입력 (YOUR_TUNNEL_ID를 실제 Tunnel ID로 변경):
```yaml
tunnel: YOUR_TUNNEL_ID
credentials-file: /root/.cloudflared/YOUR_TUNNEL_ID.json

ingress:
  - hostname: YOUR_SUBDOMAIN.yourdomain.com
    service: http://localhost:5000
  - service: http_status:404
```

### 4.5 DNS 레코드 연결
```bash
# 서브도메인을 Tunnel에 연결
cloudflared tunnel route dns exceltool YOUR_SUBDOMAIN.yourdomain.com
```

### 4.6 Cloudflared 서비스 설치 및 시작
```bash
# 서비스로 설치
sudo cloudflared service install

# 서비스 시작
sudo systemctl start cloudflared

# 서비스 활성화 (부팅 시 자동 시작)
sudo systemctl enable cloudflared

# 상태 확인
sudo systemctl status cloudflared
```

## 5단계: 배포 완료 확인

### 5.1 로컬 테스트
```bash
# 서버에서
curl http://localhost:5000
```

### 5.2 Cloudflare Tunnel 테스트
브라우저에서 `https://YOUR_SUBDOMAIN.yourdomain.com` 접속

## 업데이트 방법

### 자동 배포 스크립트 사용
```bash
# 로컬(WSL)에서 실행
cd /mnt/c/dev/ExcelLinkExtractor
./deploy.sh
```

### 수동 배포
```bash
# 1. 로컬에서 빌드
dotnet publish ExcelLinkExtractorWeb/ExcelLinkExtractorWeb.csproj -c Release -o ./publish

# 2. 서버로 전송
scp -r ./publish/* user@192.168.0.8:/var/www/excellinkextractor/

# 3. 서버에서 서비스 재시작
ssh user@192.168.0.8 "sudo systemctl restart excellinkextractor"
```

## 문제 해결

### 애플리케이션이 시작되지 않는 경우
```bash
# 로그 확인
sudo journalctl -u excellinkextractor -n 50

# 수동 실행 테스트
cd /var/www/excellinkextractor
dotnet ExcelLinkExtractorWeb.dll
```

### Cloudflare Tunnel이 연결되지 않는 경우
```bash
# Tunnel 상태 확인
sudo systemctl status cloudflared
sudo journalctl -u cloudflared -n 50

# 수동 실행 테스트
cloudflared tunnel run exceltool
```

### 포트 변경이 필요한 경우
`appsettings.Production.json` 파일을 수정하고 서비스를 재시작:
```bash
sudo systemctl restart excellinkextractor
```

## 보안 권장사항

1. **방화벽 설정**: 5000 포트는 로컬호스트만 접근 가능하게 설정
2. **HTTPS 강제**: Cloudflare에서 "Always Use HTTPS" 활성화
3. **WAF 설정**: Cloudflare WAF로 악성 요청 차단
4. **Rate Limiting**: Cloudflare에서 Rate Limiting 규칙 설정

## 유용한 명령어

```bash
# 서비스 상태 확인
sudo systemctl status excellinkextractor

# 서비스 재시작
sudo systemctl restart excellinkextractor

# 로그 실시간 모니터링
sudo journalctl -u excellinkextractor -f

# Cloudflare Tunnel 상태
sudo systemctl status cloudflared

# 디스크 사용량 확인
du -sh /var/www/excellinkextractor
```
