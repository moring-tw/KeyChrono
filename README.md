# KeyChrono
A customizable, multi-instance countdown timer for Windows 10/11 built with C# WPF. Features global hotkeys, frameless transparent overlays, auto-restart, and auto-saving. / 一款專為 Windows 10/11 打造的無邊框多重倒數計時器，支援全域快捷鍵、透明去背圖片、閃爍提醒與自動存檔功能

![License](https://img.shields.io/badge/License-MIT-blue.svg)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-lightgrey.svg)
![Framework](https://img.shields.io/badge/Framework-WPF%20%2B%20.NET-5C2D91.svg)

---

## ✨ 功能特色 (Features)

* **⌨️ 全域快捷鍵啟動/停止**：在任何背景軟體下，按下設定的快捷鍵 (如 `F5`, `Ctrl+Shift+A`) 即可瞬間喚醒或關閉計時器。
* **🖼️ 無邊框透明置頂 UI**：原生支援帶有透明通道的 PNG / GIF 圖片，無邊框且強制置頂 (TopMost)，完美融入桌面與實況畫面。
* **🔀 多執行緒獨立計時**：可以設定多組不同快捷鍵，同時在螢幕不同座標顯示多個獨立的倒數計時器。
* **⚠️ 視覺閃爍提醒**：倒數至最後 5 秒時，圖片會自動閃爍提醒。
* **🔁 自動重置模式**：支援「倒數完畢自動重啟」，適合需要循環計時的場景。
* **💾 參數自動儲存**：所有的設定與清單會自動以 JSON 格式儲存於 `%AppData%`，下次開啟程式無須重新設定。
* **🖱️ 快速編輯與管理**：點擊清單即可將參數帶入輸入框進行修改或刪除。

---

## 🚀 安裝與執行 (Installation & Usage)

### 系統需求
* Windows 10 或 Windows 11
* [.NET Desktop Runtime](https://dotnet.microsoft.com/download) (若執行出現提示請安裝)

### 編譯指南 (給開發者)
1. 將本專案 Clone 至本地端
2. 進入專案資料夾，雙擊開啟 CountdownApp.sln 方案檔。
3. 還原 NuGet 套件 (非常重要)：
方法 A (Visual Studio)：在右側「方案總管」對專案點擊右鍵 ➔ 選擇 「管理 NuGet 套件」 ➔ Visual Studio 通常會自動提示並還原缺失的套件。
方法 B (指令碼)：打開上方的「工具」➔「NuGet 套件管理員」➔「套件管理員主控台」，輸入以下指令強制還原：
```
Update-Package -reinstall
```
確認沒有錯誤提示後，按下 F5 或點擊「開始」即可編譯並執行。

## 📦 依賴套件與依賴項 (Dependencies & NuGet Packages)
本專案依賴以下第三方與原生套件，若手動建置專案，請確保已透過 NuGet 安裝：
NHotkey.Wpf (v2.1.0 或以上)
用途：提供極其簡便的 WPF 全域快捷鍵 (Global Hotkeys) 註冊與攔截功能。
安裝指令：Install-Package NHotkey.Wpf
System.Text.Json
用途：微軟原生高效能的 JSON 序列化工具，用於將設定檔寫入/讀取 AppData。
註：在較新的 .NET 核心中已內建，若使用較舊的 .NET Framework 則需另外透過 NuGet 安裝 (Install-Package System.Text.Json)。