<div align="center">
<img src="./ClickyKeys.Packaging/Images/Icon.png" width="10%" > 

# ClickyKeys

**A free, open-source key press and click counter for gamers and streamers.**

[![Microsoft Store](https://img.shields.io/badge/Microsoft%20Store-Download-blue?logo=microsoft)](https://apps.microsoft.com/store/detail/9PJT83WPC06K?cid=DevShareMCLPCS)
[![GitHub Release](https://img.shields.io/github/v/release/Reksaku/ClickyKeys)](https://github.com/Reksaku/ClickyKeys/releases/latest)
[![License: GPL-3.0](https://img.shields.io/badge/License-GPL--3.0-green)](LICENSE)
![Users](https://img.shields.io/badge/Users-500%2B-brightgreen)

</div>

<h3>

ClickyKeys counts how many times you press chosen keys or mouse buttons and displays them in a customizable panel — perfect for streams, recordings, and geeky satisfaction.

<br>
<div align="center">
  <picture>
      <img src="./Resources/Images/Animation.gif" width="50%">
  </picture>
</div>

---

## ✨ Features

<h4>
<img src="./Resources/Images/screenshot-cream.png" width="50%" align="right">

**🎯 Count any key or mouse button**
- Pick exactly which keys and mouse buttons to track.
- Give each key its own custom label (e.g. *Jump*, *Reload*, *LMB*).
- Counters update in real time.

<br>

<br>

**🎨 Fully customizable panel**



- Choose which keys to display.
- Adjust button size and grid layout.
- Change text and background colors.
- Select your favourite font and size.

<div style="clear: both;"></div>

---

## 💾 Save & load style profiles

Swap profiles with a single click. Game-specific sets are always ready in your library.

<br>
<div align="center">
  <picture>
    <img src="./Resources/Images/Profile_picker.png" width="40%">
  </picture>
</div>

---

## 🌈 Rainbow theme

Bored with a static background? Enable the animated rainbow theme and bring more colour to your stream.

<br>
<div align="center">
  <picture>
      <img src="./Resources/Images/Rainbow_animation.gif" width="50%">
  </picture>
</div>

---

## 📊 Local key press statistics *(new in v2.3.0)*

Track your long-term key usage with built-in local statistics. All data stays on your device — no cloud, no account needed.

<br>
<div align="center">
  <picture>
      <!-- Add screenshot: ./Resources/Images/screenshot-stats.png -->
      <img src="./Resources/Images/screenshot-stats.png" width="30%">
  </picture>
</div>
<br>

---


## 🏪 Available on Microsoft Store & GitHub

<h3>

[<img src="https://upload.wikimedia.org/wikipedia/commons/4/44/Microsoft_logo.svg" width="14"/> Microsoft Store — one-click install](https://apps.microsoft.com/store/detail/9PJT83WPC06K?cid=DevShareMCLPCS)

[<img src="https://github.githubassets.com/images/modules/logos_page/GitHub-Mark.png" width="14"/> GitHub Releases — portable version, no install needed](https://github.com/Reksaku/ClickyKeys/releases/latest)

> ###### If Windows SmartScreen flags the portable file, click **More info → Run anyway**. This is a common false positive for small unsigned C# apps — the full source code is available here on GitHub.

---

## 🎬 OBS integration

ClickyKeys works great with OBS. You can either:
- Use **chroma key** on the background colour for a clean transparent overlay, or
- Enable **transparent mode** to overlay the panel directly without chroma key.

<br>
<div align="center">
  <picture>
      <img src="./Resources/Images/Example_in_game.png" width="50%">
  </picture>
</div>

---

## 🖼 Screenshots

Colors, fonts, grid layout — everything is yours to configure.

<div align="center">

| Dark | Blue | Orange |
|------|------|--------|
| <img src="./Resources/Images/screenshot-dark.png" width="250"> | <img src="./Resources/Images/screenshot-blue.png" width="250"> | <img src="./Resources/Images/screenshot-orange.png" width="250"> |

<!-- Add screenshots to ./Resources/Images/ — originals available at https://clickykeys.fun -->

</div>


---

## 🛠 Usage

1. Download and run ClickyKeys (Microsoft Store or GitHub Releases).
2. Click the panel and choose the keys you want to track.
3. Set a custom label for each button (e.g. *Jump*, *Reload*).
4. Smash your keyboard (or click around) and watch the counters go up!
5. Reset all counters anytime with **F12**.
6. *(Optional)* In OBS, add a Window Capture source for the panel and use chroma key or transparent mode.

---

## 📋 What's new

| Version | Highlights |
|---------|-----------|
| **v2.3.0** | Local key press statistics — track long-term usage, all data stored on-device |
| **v2.2.1** | Save & load style profiles — switch configurations instantly between games |
| **v2.1.1** | Auto-update notifications — the app notifies you when a new version is out |
| **v2.1.0** | Rainbow theme & font customization — animated background + font family/size control |
| **v2.0.0** | Full rewrite in WPF — better performance, stability, and a modern interface |

---

## ❓ FAQ

**Does ClickyKeys store or send my data?**  
No. ClickyKeys only counts key presses and mouse clicks locally and shows them in the panel. No data is transmitted anywhere. The code is fully open — you can verify it yourself.

**Which systems are supported?**  
Windows (desktop app). If you want to help with ports to other systems, reach out via GitHub Issues.

**Is ClickyKeys free?**  
Yes. ClickyKeys is completely free and open-source under the **GPL-3.0** license.

**How do I use it with OBS?**  
Run ClickyKeys and select the keys to track. In OBS, add a Window Capture source pointing to the ClickyKeys panel. Use the built-in background colour for chroma key, or enable transparent mode to overlay directly.

**I found a bug or have a feature idea.**  
Open an issue in this repository — contributions and feedback are welcome!

---

## 💡 Why?

Because pressing buttons is fun, and sometimes you just want to know how many times you did it.

A small side project made for simple geeky satisfaction. 

---

## 🔧 Planned / Ideas

- More advanced statistics 📊
