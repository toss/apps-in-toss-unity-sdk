#!/bin/bash
# 메모리 제한 컨테이너 안에서 헤드리스 Chromium 과 CDP 포워더를 띄운다(memlimit.mjs 가 docker run 으로 호출).
# 환경변수: HOST_ALIAS(localhost 를 매핑할 호스트 이름), EXTRA(추가 Chromium 플래그)
CH=$(ls -d /ms-playwright/chromium_headless_shell-*/chrome-headless-shell-linux*/chrome-headless-shell | head -n1)
node /m/cdp-forward.mjs &
exec "$CH" --disable-field-trial-config --disable-background-networking --disable-background-timer-throttling \
  --disable-backgrounding-occluded-windows --disable-back-forward-cache --disable-breakpad \
  --disable-client-side-phishing-detection --disable-component-extensions-with-background-pages \
  --disable-component-update --no-default-browser-check --disable-default-apps --disable-dev-shm-usage \
  --disable-extensions --disable-hang-monitor --disable-ipc-flooding-protection --disable-popup-blocking \
  --disable-prompt-on-repost --disable-renderer-backgrounding --force-color-profile=srgb \
  --metrics-recording-only --no-first-run --password-store=basic --use-mock-keychain --disable-infobars \
  --disable-sync --enable-unsafe-swiftshader --headless --hide-scrollbars --mute-audio --no-sandbox \
  --enable-webgl --use-angle=default --user-data-dir=/tmp/prof \
  --host-resolver-rules="MAP localhost ${HOST_ALIAS}" \
  --remote-debugging-port=9222 $EXTRA about:blank
