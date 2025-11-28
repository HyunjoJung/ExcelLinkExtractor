# 빠른 배포 가이드

이 가이드는 최소한의 단계로 192.168.0.8 서버에 배포하는 방법을 설명합니다.

## 준비물

- SSH 접근: root@192.168.0.8:22 (비밀번호: 1234)
- Cloudflare 계정
- 도메인 (Cloudflare에 등록되어 있어야 함)

## 배포 순서

### 1단계: sshpass 설치 (로컬/WSL)

배포 스크립트가 자동으로 SSH 비밀번호를 입력하기 위해 필요합니다.

```bash
# WSL/Ubuntu에서
sudo apt-get update
sudo apt-get install -y sshpass
```

### 2단계: 서버 초기 설정

서버에 SSH 접속하여 필요한 소프트웨어를 설치합니다.

```bash
# 서버 접속
ssh root@192.168.0.8

# setup 스크립트 다운로드 (또는 수동으로 복사)
# 아래 명령어를 서버에서 실행하거나, 로컬에서 scp로 전송
```

또는 로컬에서:
```bash
# 로컬(WSL)에서 setup 스크립트 전송
sshpass -p "1234" scp -o StrictHostKeyChecking=no setup-server.sh root@192.168.0.8:/root/

# 서버에서 실행
sshpass -p "1234" ssh -o StrictHostKeyChecking=no root@192.168.0.8 "bash /root/setup-server.sh"
```

### 3단계: Cloudflare Tunnel 설정 (서버에서)

```bash
# 서버에 접속
ssh root@192.168.0.8

# Cloudflare 인증
cloudflared tunnel login
```

브라우저가 열리면 Cloudflare 계정 로그인 → 도메인 선택

```bash
# Tunnel 생성
cloudflared tunnel create exceltool

# Tunnel ID 확인 및 기록
cloudflared tunnel list
# 출력 예: 12345678-1234-1234-1234-123456789abc
```

Tunnel ID를 복사해두세요!

### 4단계: Cloudflare Tunnel 설정 파일 생성

```bash
# 서버에서 실행
mkdir -p /etc/cloudflared
nano /etc/cloudflared/config.yml
```

다음 내용을 입력 (YOUR_TUNNEL_ID와 도메인 수정):

```yaml
tunnel: YOUR_TUNNEL_ID
credentials-file: /root/.cloudflared/YOUR_TUNNEL_ID.json

ingress:
  - hostname: excel.yourdomain.com
    service: http://localhost:5000
  - service: http_status:404
```

저장: Ctrl+O, Enter, Ctrl+X

### 5단계: DNS 연결 (서버에서)

```bash
# 서브도메인을 Tunnel에 연결
cloudflared tunnel route dns exceltool excel.yourdomain.com
```

> 참고: `excel.yourdomain.com`을 원하는 서브도메인으로 변경하세요

### 6단계: Cloudflare Tunnel 서비스 시작

```bash
# 서버에서 실행
cloudflared service install
systemctl start cloudflared
systemctl enable cloudflared
systemctl status cloudflared
```

### 7단계: 애플리케이션 배포 (로컬/WSL)

```bash
# 로컬(WSL)에서 실행
cd /mnt/c/dev/ExcelLinkExtractor
./deploy.sh
```

배포 스크립트가 자동으로:
1. 프로젝트 빌드
2. 서버로 파일 전송
3. systemd 서비스 설정
4. 애플리케이션 시작

### 8단계: 확인

브라우저에서 `https://excel.yourdomain.com` 접속!

## 문제 해결

### 애플리케이션이 시작되지 않는 경우

```bash
# 서버에서 로그 확인
ssh root@192.168.0.8
journalctl -u excellinkextractor -n 50 --no-pager
```

### Cloudflare Tunnel이 연결되지 않는 경우

```bash
# 서버에서 Tunnel 상태 확인
ssh root@192.168.0.8
systemctl status cloudflared --no-pager
journalctl -u cloudflared -n 50 --no-pager
```

### .NET 10이 설치되지 않는 경우

.NET 10은 2024년 11월에 정식 릴리스되었습니다. 아직 Ubuntu 리포지토리에 없을 수 있습니다.

해결 방법 1: .NET 8 사용
```bash
# ExcelLinkExtractorWeb.csproj 수정
<TargetFramework>net8.0</TargetFramework>

# 프로젝트 재빌드 후 배포
```

해결 방법 2: 최신 .NET SDK 수동 설치
```bash
# 서버에서
wget https://dot.net/v1/dotnet-install.sh
bash dotnet-install.sh --channel 10.0 --runtime aspnetcore
```

## 업데이트 방법

코드를 수정한 후:

```bash
cd /mnt/c/dev/ExcelLinkExtractor
./deploy.sh
```

## 유용한 명령어

```bash
# 애플리케이션 로그 실시간 보기
ssh root@192.168.0.8 "journalctl -u excellinkextractor -f"

# 애플리케이션 재시작
ssh root@192.168.0.8 "systemctl restart excellinkextractor"

# Cloudflare Tunnel 재시작
ssh root@192.168.0.8 "systemctl restart cloudflared"

# 로컬에서 서버 테스트 (Cloudflare 없이)
curl http://192.168.0.8:5000
```

## 서브도메인 예시

사용 가능한 서브도메인:
- `excel.yourdomain.com`
- `links.yourdomain.com`
- `tool.yourdomain.com`
- `exceltools.yourdomain.com`

원하는 서브도메인을 선택하고 4, 5단계에서 해당 도메인을 사용하세요.
