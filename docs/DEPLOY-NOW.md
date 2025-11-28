# 지금 바로 배포하기

도메인: **https://excellink.hyunjo.uk**

## 3단계로 끝내는 배포

### 1단계: sshpass 설치 (1분)

```bash
sudo apt-get update && sudo apt-get install -y sshpass
```

### 2단계: 서버 설정 (5분)

```bash
cd /mnt/c/dev/ExcelLinkExtractor

# 서버 초기 설정
sshpass -p "1234" scp -o StrictHostKeyChecking=no setup-server.sh root@192.168.0.8:/root/
sshpass -p "1234" ssh -o StrictHostKeyChecking=no root@192.168.0.8 "bash /root/setup-server.sh"

# Cloudflare Tunnel 설정
sshpass -p "1234" scp -o StrictHostKeyChecking=no cloudflare-tunnel-setup.sh root@192.168.0.8:/root/
sshpass -p "1234" ssh -o StrictHostKeyChecking=no root@192.168.0.8 "bash /root/cloudflare-tunnel-setup.sh"
```

> 브라우저가 열리면 Cloudflare 계정 로그인 → `hyunjo.uk` 도메인 선택

### 3단계: 애플리케이션 배포 (2분)

```bash
./deploy.sh
```

## 완료!

브라우저에서 접속: **https://excellink.hyunjo.uk**

---

## 문제 해결

### "cloudflared tunnel login" 에서 브라우저가 안 열리는 경우

수동으로 URL 복사해서 브라우저에 붙여넣기

### .NET 10이 설치 안 되는 경우

프로젝트를 .NET 8로 변경:

```bash
# ExcelLinkExtractorWeb/ExcelLinkExtractorWeb.csproj 수정
# <TargetFramework>net10.0</TargetFramework>
# →
# <TargetFramework>net8.0</TargetFramework>
```

### 로그 확인

```bash
# 애플리케이션 로그
ssh root@192.168.0.8 "journalctl -u excellinkextractor -f"

# Cloudflare Tunnel 로그
ssh root@192.168.0.8 "journalctl -u cloudflared -f"
```

### 재배포

코드 수정 후:

```bash
./deploy.sh
```

끝!
