# 🤖 Arena & PowerX Discord Bot

Discord bot designed to manage licenses and authenticate users directly through Discord.

---

## ⚡ Features

- **`/genkey [days] [count] [user] [note]`**: Generate 1 or multiple license keys. Automatically sends a private DM to the tagged user!
- **`/checkkey [key]`**: View key validity, status (Available/Used/Banned), and creation date.
- **`/resethwid [key]`**: Clear hardware ID lock on demand.
- **`/extendkey [key] [days]`**: Add extra days to a license.
- **`/delkey [key]`**: Delete a license from the server.
- **`/stats`**: View total keys, applications, and registered users.

---

## 🚀 Setup Guide

### 1. Create Bot on Discord Developer Portal
1. Go to [Discord Developer Portal](https://discord.com/developers/applications).
2. Click **New Application** (e.g. `Arena Auth Bot`).
3. In **Bot** tab:
   - Click **Reset Token** and copy the **Bot Token**.
   - Under **Privileged Gateway Intents**, enable:
     - **Server Members Intent**
     - **Message Content Intent**
4. In **OAuth2** tab:
   - Copy your **Client ID** (Application ID).
   - Under **URL Generator**, check:
     - Scopes: `bot`, `applications.commands`
     - Bot Permissions: `Administrator` (or `Send Messages`, `Embed Links`, `Use Slash Commands`).
   - Copy the generated URL and paste it in your browser to invite the bot to your Discord server.

### 2. Configure `.env`
Edit `DiscordBot/.env`:
```env
DISCORD_TOKEN=your_token_here
CLIENT_ID=your_client_id_here
GUILD_ID=your_server_id_here   # (Recommended: gives instant slash commands without delay)
API_BASE_URL=http://localhost:5000/api/bot
BOT_SECRET=zyrex-bot-9f4c2e7a1b8d6f3e5c0a7b4d2f8e1c6a
```

### 3. Launch
Double click `start-bot.bat` or run:
```bash
node index.js
```
