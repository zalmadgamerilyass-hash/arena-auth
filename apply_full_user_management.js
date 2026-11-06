const fs = require('fs');
const path = require('path');

// 1. UPDATE AdminController.cs
const ctrlPath = path.join('BlackoutAuth.Server', 'Controllers', 'AdminController.cs');
let ctrl = fs.readFileSync(ctrlPath, 'utf8');

// Update GetUsers to include appName
const getUsersTarget = `            var users = await userQuery.OrderByDescending(u => u.CreatedAt).Take(200).ToListAsync();

            var sanitized = users.Select(u => new
            {
                u.Id,
                u.AppId,`;

const getUsersReplacement = `            var users = await userQuery.OrderByDescending(u => u.CreatedAt).Take(200).ToListAsync();
            var appMap = await _db.Applications.ToDictionaryAsync(a => a.Id, a => a.Name);

            var sanitized = users.Select(u => new
            {
                u.Id,
                u.AppId,
                appName = appMap.TryGetValue(u.AppId, out var name) ? name : "App",`;

if (ctrl.includes(getUsersTarget)) {
  ctrl = ctrl.replace(getUsersTarget, getUsersReplacement);
}

// Update ResetHwid, BanUser, ExtendUser, DeleteUser to give VINI full bypass
const bypassTarget1 = `            // Verify the user belongs to an app owned by this admin
            var app = await _db.Applications.FirstOrDefaultAsync(a => a.Id == user.AppId && a.AdminUsername == adminUser);
            if (app == null) return Forbid();`;

const bypassReplace1 = `            if (!adminUser.Equals("VINI", StringComparison.OrdinalIgnoreCase))
            {
                var app = await _db.Applications.FirstOrDefaultAsync(a => a.Id == user.AppId && a.AdminUsername == adminUser);
                if (app == null) return Forbid();
            }`;

ctrl = ctrl.replace(bypassTarget1, bypassReplace1);

const bypassTargetOther = `            var app = await _db.Applications.FirstOrDefaultAsync(a => a.Id == user.AppId && a.AdminUsername == adminUser);
            if (app == null) return Forbid();`;

// Replace in BanUser, ExtendUser, DeleteUser
ctrl = ctrl.replaceAll(bypassTargetOther, bypassReplace1);

fs.writeFileSync(ctrlPath, ctrl, 'utf8');
console.log('AdminController.cs updated with full VINI super admin powers.');

// 2. UPDATE index.html
const indexPath = path.join('BlackoutAuth.Server', 'wwwroot', 'index.html');
let indexHtml = fs.readFileSync(indexPath, 'utf8');

// Change sidebar button label
indexHtml = indexHtml.replace(
  `<span>Users</span>\r\n          </button>`,
  `<span>Users & Passwords</span>\r\n          </button>`
);
indexHtml = indexHtml.replace(
  `<span>Users</span>\n          </button>`,
  `<span>Users & Passwords</span>\n          </button>`
);

// Add embedded Quick User Creation form at top of tab-users
const tabUsersTarget = `<div id="tab-users" class="tab-pane hidden space-y-6">
          <div class="flex flex-col md:flex-row md:items-center justify-between gap-4">
            <div>
              <h3 class="text-lg font-bold text-white">Registered Users</h3>
              <p class="text-xs text-slate-400 font-mono mt-0.5">Manage accounts, set expiry, device limits, reset HWID</p>
            </div>
            <div class="flex items-center space-x-3">
              <input type="text" id="userSearch" oninput="loadUsers()" placeholder="Search user, HWID..." class="search-input">
              <button onclick="openUserCreateModal()" class="btn-primary">
                <i class="fa-solid fa-user-plus text-xs"></i>
                <span>Create User</span>
              </button>
            </div>
          </div>`;

const tabUsersReplacement = `<div id="tab-users" class="tab-pane hidden space-y-6">
          <div class="flex flex-col md:flex-row md:items-center justify-between gap-4">
            <div>
              <h3 class="text-lg font-bold text-white flex items-center gap-2">
                <i class="fa-solid fa-users-gear text-purple-400"></i>
                <span>Users & Passwords Management</span>
              </h3>
              <p class="text-xs text-slate-400 font-mono mt-0.5">Create users with passwords, set expiry, and manage all accounts</p>
            </div>
            <div class="flex items-center space-x-3">
              <input type="text" id="userSearch" oninput="loadUsers()" placeholder="Search user, HWID..." class="search-input">
              <button onclick="openUserCreateModal()" class="btn-primary">
                <i class="fa-solid fa-user-plus text-xs"></i>
                <span>Modal Form</span>
              </button>
            </div>
          </div>

          <!-- DIRECT USER CREATION WITH PASSWORD CARD -->
          <div class="glass-panel p-5 border border-purple-500/30 bg-gradient-to-r from-purple-950/30 via-slate-900/60 to-[#0e111a] rounded-2xl shadow-xl">
            <div class="flex items-center justify-between mb-3">
              <h4 class="text-sm font-bold text-white flex items-center gap-2">
                <span class="w-6 h-6 rounded-lg bg-purple-600/30 text-purple-300 flex items-center justify-center text-xs border border-purple-500/30"><i class="fa-solid fa-key"></i></span>
                <span>Add User With Password (إضافة مستخدم جديد مع كلمة السر)</span>
              </h4>
              <span class="text-[10px] text-purple-400 font-mono bg-purple-950/60 px-2 py-0.5 rounded border border-purple-800/40">VINI Super Admin</span>
            </div>
            <form onsubmit="handleQuickUserCreate(event)" class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-3">
              <div>
                <label class="block text-[10px] font-mono uppercase text-slate-400 mb-1">Target Application</label>
                <select id="quickUserAppId" required class="form-input text-xs py-2"></select>
              </div>
              <div>
                <label class="block text-[10px] font-mono uppercase text-slate-400 mb-1">Username (اسم المستخدم)</label>
                <input type="text" id="quickUserName" required placeholder="e.g. user1" class="form-input text-xs py-2">
              </div>
              <div>
                <label class="block text-[10px] font-mono uppercase text-slate-400 mb-1">Password (كلمة السر)</label>
                <input type="text" id="quickUserPass" required placeholder="e.g. pass123" class="form-input text-xs py-2 font-mono">
              </div>
              <div>
                <label class="block text-[10px] font-mono uppercase text-slate-400 mb-1">Subscription Duration</label>
                <select id="quickUserDuration" class="form-input text-xs py-2">
                  <option value="30">30 Days (شهر)</option>
                  <option value="90">90 Days (3 أشهر)</option>
                  <option value="365">365 Days (سنة)</option>
                  <option value="99999" selected>Lifetime (مدى الحياة)</option>
                </select>
              </div>
              <div class="flex items-end">
                <button type="submit" class="btn-primary w-full h-[36px] flex items-center justify-center gap-2 text-xs font-bold shadow-lg shadow-purple-600/30">
                  <i class="fa-solid fa-plus-circle"></i>
                  <span>Add User Now</span>
                </button>
              </div>
            </form>
          </div>`;

if (indexHtml.includes(tabUsersTarget)) {
  indexHtml = indexHtml.replace(tabUsersTarget, tabUsersReplacement);
  fs.writeFileSync(indexPath, indexHtml, 'utf8');
  console.log('index.html updated successfully.');
} else {
  console.log('tabUsersTarget not matched in index.html, checking unix endings...');
  const unixTarget = tabUsersTarget.replace(/\r\n/g, '\n');
  const unixReplace = tabUsersReplacement.replace(/\r\n/g, '\n');
  if (indexHtml.includes(unixTarget)) {
    indexHtml = indexHtml.replace(unixTarget, unixReplace);
    fs.writeFileSync(indexPath, indexHtml, 'utf8');
    console.log('index.html updated successfully (unix format).');
  }
}

// 3. UPDATE dashboard.js
const dashPath = path.join('BlackoutAuth.Server', 'wwwroot', 'js', 'dashboard.js');
let dash = fs.readFileSync(dashPath, 'utf8');

// Update populateAppSelectors to include quickUserAppId
const popTarget = `  const genSel = document.getElementById('genAppId');
  const userSel = document.getElementById('userCreateAppId');`;

const popReplace = `  const genSel = document.getElementById('genAppId');
  const userSel = document.getElementById('userCreateAppId');
  const quickUserSel = document.getElementById('quickUserAppId');`;

dash = dash.replace(popTarget, popReplace);

const popClear = `  if (genSel) genSel.innerHTML = '';
  if (userSel) userSel.innerHTML = '';`;

const popClearReplace = `  if (genSel) genSel.innerHTML = '';
  if (userSel) userSel.innerHTML = '';
  if (quickUserSel) quickUserSel.innerHTML = '';`;

dash = dash.replace(popClear, popClearReplace);

const popFill = `    if (genSel) genSel.innerHTML += \`<option value="\${app.id}">\${app.name} (\${app.version})</option>\`;
    if (userSel) userSel.innerHTML += \`<option value="\${app.id}">\${app.name} (\${app.version})</option>\`;`;

const popFillReplace = `    if (genSel) genSel.innerHTML += \`<option value="\${app.id}">\${app.name} (\${app.version})</option>\`;
    if (userSel) userSel.innerHTML += \`<option value="\${app.id}">\${app.name} (\${app.version})</option>\`;
    if (quickUserSel) quickUserSel.innerHTML += \`<option value="\${app.id}">\${app.name} (\${app.version})</option>\`;`;

dash = dash.replace(popFill, popFillReplace);

// Add handleQuickUserCreate function
const quickCreateFunc = `
async function handleQuickUserCreate(e) {
  e.preventDefault();
  const appId = document.getElementById('quickUserAppId').value;
  const username = document.getElementById('quickUserName').value.trim();
  const password = document.getElementById('quickUserPass').value;
  const durationDays = parseInt(document.getElementById('quickUserDuration').value) || 99999;

  if (!username || !password) {
    showToast('Username and password are required.', 'error');
    return;
  }

  try {
    const data = await apiFetch(\`\${API_BASE}/users/create\`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ appId, username, password, durationDays, level: 1, maxLogins: 1 })
    });

    if (data.success) {
      showToast(\`User '\${username}' created successfully!\`, 'success');
      document.getElementById('quickUserName').value = '';
      document.getElementById('quickUserPass').value = '';
      await loadUsers();
      await loadStats();
    } else {
      showToast(data.message || 'Failed to create user.', 'error');
    }
  } catch (err) {
    showToast('Error creating user account.', 'error');
  }
}
`;

if (!dash.includes('handleQuickUserCreate')) {
  dash += '\r\n' + quickCreateFunc;
}

// Add appName pill to users table in loadUsers
const tableRowTarget = `<td class="px-6 py-4 font-bold text-white flex items-center gap-2">
              <i class="fa-solid fa-user-circle text-purple-400 text-sm"></i>
              <span>\${u.username}</span>
            </td>`;

const tableRowReplace = `<td class="px-6 py-4 font-bold text-white flex items-center gap-2">
              <i class="fa-solid fa-user-circle text-purple-400 text-sm"></i>
              <span>\${u.username}</span>
              \${u.appName ? \`<span class="text-[10px] text-cyan-400 bg-cyan-950/40 border border-cyan-800/40 px-1.5 py-0.5 rounded font-mono font-normal">\${u.appName}</span>\` : ''}
            </td>`;

dash = dash.replace(tableRowTarget, tableRowReplace);

fs.writeFileSync(dashPath, dash, 'utf8');
console.log('dashboard.js updated successfully.');
