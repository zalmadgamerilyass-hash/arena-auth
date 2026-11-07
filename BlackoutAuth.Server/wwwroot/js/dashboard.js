// ARENA AUTH - ADMINISTRATIVE CONTROL SCRIPT

const API_BASE = '/api/admin';
let adminToken = localStorage.getItem('blackout_admin_token') || null;
let currentAppId = '';
let appsCache = [];
let chartInstance = null;

// Initialize on Load
document.addEventListener('DOMContentLoaded', () => {
  if (!adminToken) {
    showLoginModal();
  } else {
    hideLoginModal();
    initDashboard();
  }
});

function showLoginModal() {
  const modal = document.getElementById('loginModal');
  modal.classList.remove('hidden');
}

function hideLoginModal() {
  const modal = document.getElementById('loginModal');
  modal.classList.add('hidden');
}

async function handleLogin(e) {
  e.preventDefault();
  const user = document.getElementById('adminUser').value;
  const pass = document.getElementById('adminPass').value;
  const errDiv = document.getElementById('loginError');

  errDiv.classList.add('hidden');

  try {
    const res = await fetch(`${API_BASE}/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username: user, password: pass })
    });

    const data = await res.json();
    if (data.success) {
      adminToken = data.token;
      localStorage.setItem('blackout_admin_token', adminToken);
      document.getElementById('adminNameDisplay').textContent = data.username || 'admin';
      hideLoginModal();
      initDashboard();
      showToast('Welcome back, Commander!', 'success');
    } else {
      errDiv.textContent = data.message || 'Authorization rejected.';
      errDiv.classList.remove('hidden');
    }
  } catch (err) {
    errDiv.textContent = 'Unable to reach authentication server.';
    errDiv.classList.remove('hidden');
  }
}

function logout() {
  adminToken = null;
  localStorage.removeItem('blackout_admin_token');
  showLoginModal();
  showToast('Logged out successfully.', 'info');
}

function apiFetch(url, options = {}) {
  options.headers = options.headers || {};
  if (adminToken) {
    options.headers['X-Admin-Token'] = adminToken;
  }
  return fetch(url, options).then(async (res) => {
    if (res.status === 401) {
      logout();
      throw new Error('Unauthorized');
    }
    return res.json();
  });
}

async function initDashboard() {
  await loadApps();
  await loadStats();
  initChart();

  // Show walkthrough tour if not opted out
  if (localStorage.getItem('blackout_hide_tour') !== 'true') {
    setTimeout(openWalkthroughModal, 400);
  }

  // ANTI-SKID / WATERMARK PROTECTION: Ensure DEXTER credit cannot be removed
  setInterval(() => {
    const cred = document.getElementById('dexter-credit');
    if (!cred || !cred.innerText.includes('DEXTER')) {
      document.body.innerHTML = `
        <div style="height:100vh;display:flex;align-items:center;justify-content:center;background:#000;color:red;font-family:monospace;flex-direction:column;text-align:center;">
          <h1 style="font-size:3rem;margin-bottom:10px;">🚨 COPYRIGHT VIOLATION 🚨</h1>
          <p style="font-size:1.2rem;color:#ccc;">You attempted to remove or modify the developer credits.</p>
          <p style="font-size:1.5rem;margin-top:20px;">This framework was exclusively Developed & Engineered by <strong>DEXTER</strong>.</p>
        </div>`;
    }
  }, 2000);
}

function switchTab(tabId) {
  document.querySelectorAll('.tab-pane').forEach(el => el.classList.add('hidden'));
  document.querySelectorAll('.nav-item').forEach(el => el.classList.remove('active'));

  const activePane = document.getElementById(`tab-${tabId}`);
  if (activePane) activePane.classList.remove('hidden');

  const navBtn = document.querySelector(`.nav-item[data-tab="${tabId}"]`);
  if (navBtn) navBtn.classList.add('active');

  const titles = {
    dashboard: 'Dashboard Overview',
    apps: 'Application Management',
    keys: 'License Keys & Generation',
    users: 'Registered User Accounts',
    vars: 'Cloud Secret Variables',
    files: 'Protected Binary Distribution',
    logs: 'Security Event Stream',
    sdk: 'Client SDK Integration Guide',
    settings: 'Server & Security Settings'
  };
  document.getElementById('pageTitle').textContent = titles[tabId] || 'Dashboard';

  if (tabId === 'dashboard') loadStats();
  if (tabId === 'apps') renderApps();
  if (tabId === 'keys') loadKeys();
  if (tabId === 'users') loadUsers();
  if (tabId === 'vars') loadVars();
  if (tabId === 'files') loadFiles();
  if (tabId === 'logs') loadLogs();
  if (tabId === 'sdk') updateSdkCodeView();
}

function refreshData() {
  const icon = document.getElementById('refreshIcon');
  icon.classList.add('fa-spin');
  setTimeout(() => icon.classList.remove('fa-spin'), 600);

  const activeNav = document.querySelector('.nav-item.active');
  const tab = activeNav ? activeNav.getAttribute('data-tab') : 'dashboard';
  switchTab(tab);
}

// ================= STATS & CHART =================
async function loadStats() {
  try {
    const data = await apiFetch(`${API_BASE}/stats?appId=${currentAppId}`);
    if (data.success) {
      document.getElementById('statTotalUsers').textContent = data.stats.totalUsers;
      document.getElementById('statTotalKeys').textContent = data.stats.totalKeys;
      document.getElementById('statUnusedKeys').textContent = data.stats.unusedKeys;
      document.getElementById('statLoginsToday').textContent = data.stats.loginsToday;
      document.getElementById('statSecurityAlerts').textContent = data.stats.securityAlerts;

      updateChart(data.chart);
    }
  } catch (e) {
    console.error('Stats error:', e);
  }
}

function initChart() {
  const ctx = document.getElementById('authChart').getContext('2d');
  chartInstance = new Chart(ctx, {
    type: 'line',
    data: {
      labels: ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'],
      datasets: [
        {
          label: 'Success',
          data: [0, 0, 0, 0, 0, 0, 0],
          borderColor: '#8B5CF6',
          backgroundColor: 'rgba(139, 92, 246, 0.1)',
          tension: 0.4,
          fill: true,
          pointBackgroundColor: '#8B5CF6'
        },
        {
          label: 'Failed/Blocked',
          data: [0, 0, 0, 0, 0, 0, 0],
          borderColor: '#F43F5E',
          backgroundColor: 'rgba(244, 63, 94, 0.05)',
          tension: 0.4,
          fill: true,
          pointBackgroundColor: '#F43F5E'
        }
      ]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: { display: false }
      },
      scales: {
        x: {
          grid: { color: 'rgba(255, 255, 255, 0.04)' },
          ticks: { color: '#64748b', font: { family: 'JetBrains Mono', size: 10 } }
        },
        y: {
          grid: { color: 'rgba(255, 255, 255, 0.04)' },
          ticks: { color: '#64748b', font: { family: 'JetBrains Mono', size: 10 }, beginAtZero: true }
        }
      }
    }
  });
}

function updateChart(chartData) {
  if (!chartInstance || !chartData) return;
  chartInstance.data.labels = chartData.map(d => d.date);
  chartInstance.data.datasets[0].data = chartData.map(d => d.success);
  chartInstance.data.datasets[1].data = chartData.map(d => d.failed);
  chartInstance.update();
}

// ================= APPLICATIONS =================
async function loadApps() {
  try {
    const data = await apiFetch(`${API_BASE}/apps`);
    if (data.success) {
      appsCache = data.apps;
      populateAppSelectors();
      renderApps();
    }
  } catch (e) {
    console.error('App load error:', e);
  }
}

function populateAppSelectors() {
  const topSel = document.getElementById('appSelector');
  const genSel = document.getElementById('genAppId');
  const userSel = document.getElementById('userCreateAppId');
  const sdkSel = document.getElementById('sdkAppSelect');
  const varSel = document.getElementById('varAppId');
  const fileSel = document.getElementById('fileAppId');

  const oldVal = topSel.value;
  topSel.innerHTML = '<option value="">All Applications</option>';
  if (genSel) genSel.innerHTML = '';
  if (userSel) userSel.innerHTML = '';
  if (sdkSel) sdkSel.innerHTML = '';
  if (varSel) varSel.innerHTML = '';
  if (fileSel) fileSel.innerHTML = '';

  appsCache.forEach(app => {
    topSel.innerHTML += `<option value="${app.id}">${app.name}</option>`;
    if (genSel) genSel.innerHTML += `<option value="${app.id}">${app.name}</option>`;
    if (userSel) userSel.innerHTML += `<option value="${app.id}">${app.name}</option>`;
    if (sdkSel) sdkSel.innerHTML += `<option value="${app.id}">${app.name}</option>`;
    if (varSel) varSel.innerHTML += `<option value="${app.id}">${app.name}</option>`;
    if (fileSel) fileSel.innerHTML += `<option value="${app.id}">${app.name}</option>`;
  });

  if (oldVal && appsCache.some(a => a.id === oldVal)) {
    topSel.value = oldVal;
  }
  updateSdkCodeView();
}

function onAppFilterChange() {
  currentAppId = document.getElementById('appSelector').value;
  refreshData();
}

function renderApps() {
  const container = document.getElementById('appsList');
  if (!container) return;

  // 1. Update Top Stats
  const totalApps = appsCache.length;
  const activeApps = appsCache.filter(a => a.isEnabled).length;
  const pausedApps = appsCache.filter(a => !a.isEnabled).length;

  const statTotal = document.getElementById('appStatTotal');
  const statActive = document.getElementById('appStatActive');
  const statPaused = document.getElementById('appStatPaused');
  const statSessions = document.getElementById('appStatSessions');
  if (statTotal) statTotal.textContent = totalApps;
  if (statActive) statActive.textContent = activeApps;
  if (statPaused) statPaused.textContent = pausedApps;
  if (statSessions) statSessions.textContent = '0';

  // 2. Resolve Active App and populate Left Column Credentials
  if (!currentAppId && appsCache.length > 0) {
    currentAppId = appsCache[0].id;
  }
  const selectedApp = appsCache.find(a => a.id === currentAppId) || appsCache[0] || null;

  if (selectedApp) {
    const credName = document.getElementById('credAppName');
    const credOwner = document.getElementById('credOwnerId');
    const credSec = document.getElementById('credAppSecret');
    const credVer = document.getElementById('credAppVersion');
    const breadcrumb = document.getElementById('currentAppBreadcrumb');

    if (credName) credName.textContent = selectedApp.name;
    if (credOwner) credOwner.textContent = selectedApp.ownerId || 'OWNER-001';
    if (credSec) credSec.textContent = selectedApp.secret || '••••••••••••••••';
    if (credSec) credSec.setAttribute('data-full-secret', selectedApp.secret);
    if (credVer) credVer.textContent = selectedApp.version || '1.0';
    if (breadcrumb) breadcrumb.textContent = selectedApp.name;
    updateSnippetCode(selectedApp);
  }

  // 3. Populate appsFilterSelect if not focused
  const filterSel = document.getElementById('appsFilterSelect');
  if (filterSel) {
    const currVal = filterSel.value;
    filterSel.innerHTML = '<option value="">All Applications</option>' + 
      appsCache.map(a => `<option value="${a.id}" ${a.id === currentAppId ? 'selected' : ''}>${a.name}</option>`).join('');
  }

  // 4. Filter apps by search query
  const searchInput = document.getElementById('appSearchInput');
  const q = searchInput ? searchInput.value.trim().toLowerCase() : '';
  const filtered = appsCache.filter(a => {
    if (!q) return true;
    return a.name.toLowerCase().includes(q) || (a.ownerId && a.ownerId.toLowerCase().includes(q));
  });

  if (filtered.length === 0) {
    container.innerHTML = '<div class="p-8 text-center text-slate-500 font-mono text-xs rounded-xl bg-[#0e1017] border border-[#1b1e2a]">No applications found. Click "Create Application" to add one.</div>';
    return;
  }

  // 5. Render exact KeyAuth Cards (Image 1)
  container.innerHTML = filtered.map(app => {
    const isSelected = app.id === currentAppId;
    return `
      <div class="p-6 rounded-xl bg-[#0e1017] border border-[#1b1e2a] shadow-lg relative space-y-4 hover:border-slate-700 transition-all">
        <!-- App Header -->
        <div class="flex items-center justify-between">
          <h4 class="text-base font-bold text-white">${app.name}</h4>
          <span class="${app.isEnabled ? 'bg-[#052e16] text-[#4ade80] border border-[#166534]' : 'bg-[#3b1219] text-[#f87171] border border-[#991b1b]'} px-3 py-0.5 rounded-full text-[10px] font-bold tracking-wider uppercase">
            ${app.isEnabled ? 'ACTIVE' : 'PAUSED'}
          </span>
        </div>

        <!-- 3 Column Metrics -->
        <div class="grid grid-cols-3 gap-2 py-2 text-center border-y border-[#1a1d28]/70">
          <div>
            <div class="text-[11px] text-slate-400 font-medium">App Version</div>
            <div class="text-sm font-bold text-white mt-0.5">${app.version || '1.0'}</div>
          </div>
          <div>
            <div class="text-[11px] text-slate-400 font-medium">Users</div>
            <div class="text-sm font-bold text-white mt-0.5">0</div>
          </div>
          <div>
            <div class="text-[11px] text-slate-400 font-medium">Application Standing</div>
            <div class="text-sm font-bold text-emerald-400 mt-0.5">Good</div>
          </div>
        </div>

        <!-- Horizontal Action Buttons (Image 1) -->
        <div class="flex flex-wrap items-center gap-2 pt-1">
          <!-- Selected button -->
          <button onclick="selectActiveApp('${app.id}')" class="${isSelected ? 'bg-[#15803d]' : 'bg-[#1e293b] hover:bg-[#334155]'} text-white text-xs font-semibold px-3 py-1.5 rounded-lg flex items-center gap-1.5 transition-colors">
            <i class="fa-solid fa-file-lines text-xs"></i>
            <span>${isSelected ? 'Selected' : 'Select'}</span>
          </button>

          <!-- Rename button -->
          <button onclick="renameAppPrompt('${app.id}', '${app.name}')" class="bg-[#9333ea] hover:bg-[#7e22ce] text-white text-xs font-semibold px-3 py-1.5 rounded-lg flex items-center gap-1.5 transition-colors">
            <i class="fa-solid fa-pen text-xs"></i>
            <span>Rename</span>
          </button>

          <!-- Edit Description button -->
          <button onclick="editAppPrompt('${app.id}')" class="bg-[#0284c7] hover:bg-[#0369a1] text-white text-xs font-semibold px-3 py-1.5 rounded-lg flex items-center gap-1.5 transition-colors">
            <i class="fa-solid fa-pen-to-square text-xs"></i>
            <span>Edit Description</span>
          </button>

          <!-- Pause button -->
          <button onclick="toggleAppPause('${app.id}')" class="bg-[#ea580c] hover:bg-[#c2410c] text-white text-xs font-semibold px-3 py-1.5 rounded-lg flex items-center gap-1.5 transition-colors">
            <i class="fa-solid fa-pause text-xs"></i>
            <span>${app.isEnabled ? 'Pause' : 'Resume'}</span>
          </button>

          <!-- Delete button -->
          <button onclick="deleteAppConfirm('${app.id}')" class="bg-[#dc2626] hover:bg-[#b91c1c] text-white text-xs font-semibold px-3 py-1.5 rounded-lg flex items-center gap-1.5 transition-colors">
            <i class="fa-solid fa-trash-can text-xs"></i>
            <span>Delete</span>
          </button>
        </div>
      </div>
    `;
  }).join('');
}

function selectActiveApp(appId) {
  currentAppId = appId;
  const topSel = document.getElementById('appSelector');
  if (topSel) topSel.value = appId;
  renderApps();
  loadStats();
  const app = appsCache.find(a => a.id === appId);
  if (app) showToast(`Switched to '${app.name}'`, 'info');
}

function handleAppSelectChange(appId) {
  if (appId) {
    selectActiveApp(appId);
  } else {
    currentAppId = '';
    renderApps();
  }
}

function copyElementText(elId) {
  const el = document.getElementById(elId);
  if (!el) return;
  const fullText = el.getAttribute('data-full-secret') || el.textContent;
  navigator.clipboard.writeText(fullText).then(() => {
    showToast('Copied to clipboard!', 'success');
  }).catch(() => {
    copyToClipboard(fullText);
  });
}

async function refreshCurrentAppSecret() {
  const app = appsCache.find(a => a.id === currentAppId) || appsCache[0];
  if (!app) return;
  try {
    const res = await apiFetch(`${API_BASE}/apps/${app.id}/regen-secret`, { method: 'POST' });
    if (res.success) {
      showToast('Application secret refreshed successfully!', 'success');
      await loadApps();
    }
  } catch (err) {
    showToast('Failed to refresh secret.', 'error');
  }
}

async function toggleAppPause(appId) {
  const app = appsCache.find(a => a.id === appId);
  if (!app) return;
  try {
    const res = await apiFetch(`${API_BASE}/apps/${app.id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        name: app.name,
        version: app.version,
        isEnabled: !app.isEnabled,
        hwidLockEnabled: app.hwidLockEnabled,
        downloadUrl: app.downloadUrl,
        discordWebhookUrl: app.discordWebhookUrl
      })
    });
    if (res.success) {
      showToast(`Application ${!app.isEnabled ? 'activated' : 'paused'}!`, 'success');
      await loadApps();
    }
  } catch (err) {
    showToast('Failed to update app status.', 'error');
  }
}

async function renameAppPrompt(appId, oldName) {
  const newName = prompt('Enter new application name:', oldName);
  if (!newName || newName.trim() === '' || newName === oldName) return;
  const app = appsCache.find(a => a.id === appId);
  if (!app) return;
  try {
    const res = await apiFetch(`${API_BASE}/apps/${app.id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        name: newName.trim(),
        version: app.version,
        isEnabled: app.isEnabled,
        hwidLockEnabled: app.hwidLockEnabled,
        downloadUrl: app.downloadUrl,
        discordWebhookUrl: app.discordWebhookUrl
      })
    });
    if (res.success) {
      showToast(`Application renamed to '${newName}'`, 'success');
      await loadApps();
    }
  } catch (err) {
    showToast('Failed to rename application.', 'error');
  }
}

function editAppPrompt(appId) {
  const app = appsCache.find(a => a.id === appId);
  if (!app) return;
  const newVer = prompt('Update application version:', app.version || '1.0');
  if (newVer === null) return;
  apiFetch(`${API_BASE}/apps/${app.id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      name: app.name,
      version: newVer.trim() || '1.0',
      isEnabled: app.isEnabled,
      hwidLockEnabled: app.hwidLockEnabled,
      downloadUrl: app.downloadUrl,
      discordWebhookUrl: app.discordWebhookUrl
    })
  }).then(res => {
    if (res.success) {
      showToast('Application updated.', 'success');
      loadApps();
    }
  }).catch(() => showToast('Update failed.', 'error'));
}

async function deleteAppConfirm(appId) {
  const app = appsCache.find(a => a.id === appId);
  if (!app) return;
  if (!confirm(`Are you sure you want to permanently delete '${app.name}' and all its users & keys?`)) return;
  try {
    const res = await apiFetch(`${API_BASE}/apps/${app.id}`, { method: 'DELETE' });
    if (res.success) {
      showToast('Application deleted.', 'success');
      currentAppId = '';
      await loadApps();
    }
  } catch (e) {
    showToast('Failed to delete application.', 'error');
  }
}

function toggleCodeSnippetPreview() {
  const chk = document.getElementById('toggleCodeSnippet');
  const box = document.getElementById('codeSnippetBox');
  if (!chk || !box) return;
  if (chk.checked) {
    box.classList.remove('hidden');
    const app = appsCache.find(a => a.id === currentAppId) || appsCache[0];
    updateSnippetCode(app);
  } else {
    box.classList.add('hidden');
  }
}

function updateSnippetCode(app) {
  const pre = document.getElementById('codeSnippetPre');
  if (!pre || !app) return;
  pre.textContent = `using ArenaAuth.SDK.CSharp;\n\nvar auth = new ArenaApp(\n    name: "${app.name}",\n    ownerId: "${app.ownerId || 'OWNER-001'}",\n    secret: "${app.secret}",\n    version: "${app.version || '1.0'}",\n    apiUrl: "${window.location.origin}"\n);\n\nauth.Init();`;
}

async function handleAppCreate(e) {
  e.preventDefault();
  const name = document.getElementById('newAppName').value;
  const owner = document.getElementById('newAppOwner').value;
  const version = document.getElementById('newAppVersion').value;
  const download = document.getElementById('newAppDownload').value;
  const webhook = document.getElementById('newAppWebhook').value;
  const hwidLock = document.getElementById('newAppHwidLock').checked;

  try {
    const data = await apiFetch(`${API_BASE}/apps`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        name,
        ownerId: owner,
        version,
        downloadUrl: download,
        discordWebhookUrl: webhook,
        hwidLockEnabled: hwidLock
      })
    });

    if (data.success) {
      showToast('Application created successfully!', 'success');
      closeModal('appModal');
      await loadApps();
    }
  } catch (err) {
    showToast('Failed to create application.', 'error');
  }
}

async function regenSecret(id) {
  if (!confirm('Are you sure you want to regenerate the application secret? Clients using the old secret will be disconnected.')) return;
  try {
    const data = await apiFetch(`${API_BASE}/apps/${id}/regen-secret`, { method: 'POST' });
    if (data.success) {
      showToast('New secret generated!', 'success');
      await loadApps();
    }
  } catch (e) {
    showToast('Error regenerating secret.', 'error');
  }
}

async function deleteApp(id) {
  if (!confirm('WARNING: Deleting this application will delete ALL associated licenses, users, and cloud variables! Proceed?')) return;
  try {
    const data = await apiFetch(`${API_BASE}/apps/${id}`, { method: 'DELETE' });
    if (data.success) {
      showToast('Application deleted.', 'info');
      await loadApps();
    }
  } catch (e) {
    showToast('Failed to delete application.', 'error');
  }
}

// ================= LICENSES =================
let cachedKeys = [];

async function loadKeys() {
  const search = document.getElementById('keySearch').value;
  try {
    const data = await apiFetch(`${API_BASE}/keys?appId=${currentAppId}&search=${encodeURIComponent(search)}`);
    if (data.success) {
      cachedKeys = data.keys;
      const tbody = document.getElementById('keysTableBody');
      if (cachedKeys.length === 0) {
        tbody.innerHTML = `<tr><td colspan="7" class="text-center py-8 text-slate-500">No license keys found.</td></tr>`;
        return;
      }

      tbody.innerHTML = cachedKeys.map(k => {
        const badgeClass = k.status === 'Unused' ? 'badge-unused' : (k.status === 'Used' ? 'badge-active' : (k.status === 'Paused' ? 'badge-paused' : 'badge-banned'));
        const durationText = k.durationDays >= 99999 ? 'Lifetime' : `${k.durationDays}d`;

        return `
          <tr class="hover:bg-slate-900/40 transition-colors">
            <td class="px-6 py-4">
              <span class="key-chip" onclick="copyToClipboard('${k.key}')">
                <i class="fa-regular fa-copy text-purple-400 text-xs"></i>
                <span class="font-bold text-slate-200 select-all">${k.key}</span>
              </span>
            </td>
            <td class="px-6 py-4 text-slate-300">${durationText}</td>
            <td class="px-6 py-4 text-purple-400 font-bold">Lvl ${k.level}</td>
            <td class="px-6 py-4 text-slate-300 font-mono">${k.currentUses || 0} / ${k.maxUses || 1}</td>
            <td class="px-6 py-4"><span class="${badgeClass}">${k.status}</span></td>
            <td class="px-6 py-4 text-slate-400">${k.usedByUsername || '—'}</td>
            <td class="px-6 py-4 text-slate-500">${k.note || '—'}</td>
            <td class="px-6 py-4 text-right space-x-2">
              <button onclick="toggleKeyStatus('${k.key}')" title="Pause / Resume" class="p-1 text-slate-400 hover:text-amber-400"><i class="fa-solid fa-pause text-xs"></i></button>
              <button onclick="deleteKey('${k.key}')" title="Delete Key" class="p-1 text-slate-400 hover:text-rose-400"><i class="fa-solid fa-trash-can text-xs"></i></button>
            </td>
          </tr>
        `;
      }).join('');
    }
  } catch (e) {
    console.error('Keys error:', e);
  }
}

async function handleKeyGenerate(e) {
  e.preventDefault();
  const appId = document.getElementById('genAppId').value;
  const count = parseInt(document.getElementById('genCount').value) || 1;
  const duration = parseInt(document.getElementById('genDuration').value) || 30;
  const level = parseInt(document.getElementById('genLevel').value) || 1;
  const maxUses = parseInt(document.getElementById('genMaxUses')?.value) || 1;
  const prefix = document.getElementById('genPrefix').value;
  const note = document.getElementById('genNote').value;

  try {
    const data = await apiFetch(`${API_BASE}/keys/generate`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ appId, count, durationDays: duration, level, maxUses, prefix, note })
    });

    if (data.success) {
      // Show generated keys in a modal so the user can copy them all
      const keysContainer = document.getElementById('generatedKeysText');
      if (keysContainer) {
        keysContainer.value = data.keys.join('\n');
      }
      document.getElementById('generatedKeysModal').classList.remove('hidden');
      showToast(`Generated ${data.count} keys successfully!`, 'success');
      closeModal('keyGenModal');
      await loadKeys();
      await loadStats();
    }
  } catch (err) {
    showToast('Error generating keys.', 'error');
  }
}

function copyGeneratedKeys() {
  const text = document.getElementById('generatedKeysText').value;
  if (!text) return;
  navigator.clipboard.writeText(text).then(() => {
    showToast('All keys copied to clipboard!', 'info');
  });
}

async function toggleKeyStatus(key) {
  try {
    const data = await apiFetch(`${API_BASE}/keys/${encodeURIComponent(key)}/toggle-status`, { method: 'POST' });
    if (data.success) {
      showToast(`Key status is now ${data.status}`, 'info');
      await loadKeys();
    }
  } catch (e) {
    showToast('Failed to toggle key status.', 'error');
  }
}

async function deleteKey(key) {
  if (!confirm(`Delete key ${key}?`)) return;
  try {
    const data = await apiFetch(`${API_BASE}/keys/${encodeURIComponent(key)}`, { method: 'DELETE' });
    if (data.success) {
      showToast('Key deleted.', 'info');
      await loadKeys();
      await loadStats();
    }
  } catch (e) {
    showToast('Failed to delete key.', 'error');
  }
}

function exportKeysTxt() {
  if (cachedKeys.length === 0) {
    showToast('No keys available to export.', 'warning');
    return;
  }
  const text = cachedKeys.map(k => k.key).join('\r\n');
  const blob = new Blob([text], { type: 'text/plain' });
  const a = document.createElement('a');
  a.href = URL.createObjectURL(blob);
  a.download = `arena_keys_${Date.now()}.txt`;
  a.click();
  showToast(`Exported ${cachedKeys.length} keys to .txt`, 'success');
}

// ================= USERS =================
async function loadUsers() {
  const search = document.getElementById('userSearch').value;
  try {
    const data = await apiFetch(`${API_BASE}/users?appId=${currentAppId}&search=${encodeURIComponent(search)}`);
    if (data.success) {
      const tbody = document.getElementById('usersTableBody');
      if (data.users.length === 0) {
        tbody.innerHTML = `<tr><td colspan="7" class="text-center py-8 text-slate-500">No users found.</td></tr>`;
        return;
      }

      tbody.innerHTML = data.users.map(u => {
        const isBanned = u.isBanned;
        const statusBadge = isBanned ? '<span class="badge-banned">Banned</span>' : (u.isExpired ? '<span class="badge-paused">Expired</span>' : '<span class="badge-active">Active</span>');
        const hwidStatus = u.hwid ? `<span class="text-slate-300 select-all" title="${u.hwid}">${u.hwid.substring(0, 14)}...</span>` : `<span class="text-amber-400">Unbound</span>`;

        return `
          <tr class="hover:bg-slate-900/40 transition-colors">
            <td class="px-6 py-4 font-bold text-white flex items-center gap-2">
              <i class="fa-solid fa-user-circle text-purple-400 text-sm"></i>
              <span>${u.username}</span>
            </td>
            <td class="px-6 py-4">${hwidStatus}</td>
            <td class="px-6 py-4"><span class="px-2 py-0.5 rounded bg-purple-950/40 text-purple-300 border border-purple-800/40 font-mono text-[11px]">${u.maxLogins || 1} Device${(u.maxLogins || 1) > 1 ? 's' : ''}</span></td>
            <td class="px-6 py-4 text-slate-400">${u.ipAddress || '—'}</td>
            <td class="px-6 py-4 text-slate-300">${u.expiryDate.replace('T', ' ').substring(0, 19)}</td>
            <td class="px-6 py-4 text-purple-400 font-bold">Lvl ${u.level}</td>
            <td class="px-6 py-4">${statusBadge}</td>
            <td class="px-6 py-4 text-right space-x-2">
              <button onclick="resetHwid(${u.id})" title="Reset HWID" class="btn-secondary px-2 py-1 text-[11px]"><i class="fa-solid fa-unlock-keyhole text-purple-400"></i> Reset HWID</button>
              <button onclick="toggleBanUser(${u.id}, ${!isBanned})" title="${isBanned ? 'Unban' : 'Ban'}" class="${isBanned ? 'btn-secondary' : 'btn-danger'} px-2 py-1 text-[11px]">
                <i class="fa-solid fa-ban"></i> ${isBanned ? 'Unban' : 'Ban'}
              </button>
              <button onclick="extendUser(${u.id})" title="Extend Subscription" class="btn-secondary px-2 py-1 text-[11px]"><i class="fa-solid fa-clock text-cyan-400"></i> +Days</button>
              <button onclick="deleteUser(${u.id})" title="Delete" class="p-1 text-slate-400 hover:text-rose-400"><i class="fa-solid fa-trash-can text-xs"></i></button>
            </td>
          </tr>
        `;
      }).join('');
    }
  } catch (e) {
    console.error('Users error:', e);
  }
}

async function resetHwid(id) {
  try {
    const data = await apiFetch(`${API_BASE}/users/${id}/reset-hwid`, { method: 'POST' });
    if (data.success) {
      showToast(data.message, 'success');
      await loadUsers();
    }
  } catch (e) {
    showToast('Failed to reset HWID.', 'error');
  }
}

async function toggleBanUser(id, ban) {
  const reason = ban ? prompt('Enter reason for banning user:', 'Suspicious activity / violation') : null;
  if (ban && reason === null) return;

  try {
    const data = await apiFetch(`${API_BASE}/users/${id}/ban`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ banned: ban, reason })
    });
    if (data.success) {
      showToast(data.message, 'info');
      await loadUsers();
      await loadStats();
    }
  } catch (e) {
    showToast('Failed to update ban status.', 'error');
  }
}

async function extendUser(id) {
  const daysStr = prompt('How many days to add to this subscription?', '30');
  if (!daysStr) return;
  const days = parseInt(daysStr);
  if (isNaN(days) || days <= 0) return;

  try {
    const data = await apiFetch(`${API_BASE}/users/${id}/extend`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ days })
    });
    if (data.success) {
      showToast(data.message, 'success');
      await loadUsers();
    }
  } catch (e) {
    showToast('Failed to extend subscription.', 'error');
  }
}

async function deleteUser(id) {
  if (!confirm('Are you sure you want to delete this user?')) return;
  try {
    const data = await apiFetch(`${API_BASE}/users/${id}`, { method: 'DELETE' });
    if (data.success) {
      showToast('User deleted.', 'info');
      await loadUsers();
      await loadStats();
    }
  } catch (e) {
    showToast('Failed to delete user.', 'error');
  }
}

function openUserCreateModal() {
  document.getElementById('userCreateModal').classList.remove('hidden');
}

async function handleUserCreateDirect(e) {
  e.preventDefault();
  const appId = document.getElementById('userCreateAppId').value;
  const username = document.getElementById('userCreateName').value.trim();
  const password = document.getElementById('userCreatePass').value;
  const durationDays = parseInt(document.getElementById('userCreateDuration').value) || 30;
  const level = parseInt(document.getElementById('userCreateLevel').value) || 1;
  const maxLogins = parseInt(document.getElementById('userCreateMaxLogins').value) || 1;

  try {
    const data = await apiFetch(`${API_BASE}/users/create`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ appId, username, password, durationDays, level, maxLogins })
    });

    if (data.success) {
      showToast(data.message, 'success');
      closeModal('userCreateModal');
      document.getElementById('userCreateName').value = '';
      document.getElementById('userCreatePass').value = '';
      await loadUsers();
      await loadStats();
    } else {
      showToast(data.message || 'Failed to create user.', 'error');
    }
  } catch (err) {
    showToast('Error creating user account.', 'error');
  }
}

// ================= CLOUD VARIABLES =================
async function loadVars() {
  try {
    const data = await apiFetch(`${API_BASE}/vars?appId=${currentAppId}`);
    if (data.success) {
      const tbody = document.getElementById('varsTableBody');
      if (data.variables.length === 0) {
        tbody.innerHTML = `<tr><td colspan="5" class="text-center py-8 text-slate-500">No cloud variables configured.</td></tr>`;
        return;
      }

      tbody.innerHTML = data.variables.map(v => `
        <tr class="hover:bg-slate-900/40 transition-colors">
          <td class="px-6 py-4 font-bold text-purple-400 font-mono">${v.name}</td>
          <td class="px-6 py-4 font-mono text-slate-300 max-w-xs truncate">${v.value}</td>
          <td class="px-6 py-4 text-slate-300">Level ${v.minLevel}+</td>
          <td class="px-6 py-4 text-slate-400">${v.updatedAt.replace('T', ' ').substring(0, 19)}</td>
          <td class="px-6 py-4 text-right">
            <button onclick="deleteVar(${v.id})" class="p-1 text-slate-400 hover:text-rose-400"><i class="fa-solid fa-trash-can text-xs"></i></button>
          </td>
        </tr>
      `).join('');
    }
  } catch (e) {
    console.error('Vars error:', e);
  }
}

async function handleVarSave(e) {
  e.preventDefault();
  const appId = document.getElementById('varAppId').value;
  const name = document.getElementById('varName').value;
  const value = document.getElementById('varValue').value;
  const minLevel = parseInt(document.getElementById('varMinLevel').value) || 1;

  try {
    const data = await apiFetch(`${API_BASE}/vars`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ appId, name, value, minLevel })
    });
    if (data.success) {
      showToast('Cloud variable saved!', 'success');
      closeModal('varModal');
      await loadVars();
    }
  } catch (e) {
    showToast('Failed to save variable.', 'error');
  }
}

async function deleteVar(id) {
  if (!confirm('Delete variable?')) return;
  try {
    const data = await apiFetch(`${API_BASE}/vars/${id}`, { method: 'DELETE' });
    if (data.success) {
      showToast('Variable deleted.', 'info');
      await loadVars();
    }
  } catch (e) {
    showToast('Failed to delete variable.', 'error');
  }
}

// ================= PROTECTED FILES =================
async function loadFiles() {
  try {
    const data = await apiFetch(`${API_BASE}/files?appId=${currentAppId}`);
    if (data.success) {
      const tbody = document.getElementById('filesTableBody');
      if (data.files.length === 0) {
        tbody.innerHTML = `<tr><td colspan="6" class="text-center py-8 text-slate-500">No protected files uploaded.</td></tr>`;
        return;
      }

      tbody.innerHTML = data.files.map(f => {
        const sizeKb = (f.fileSize / 1024).toFixed(1) + ' KB';
        return `
          <tr class="hover:bg-slate-900/40 transition-colors">
            <td class="px-6 py-4 font-bold text-white flex items-center gap-2">
              <i class="fa-solid fa-file-code text-cyan-400"></i>
              <span>${f.fileName}</span>
            </td>
            <td class="px-6 py-4 font-mono text-purple-400 select-all">${f.id}</td>
            <td class="px-6 py-4 text-slate-400">${sizeKb}</td>
            <td class="px-6 py-4 text-slate-300">Level ${f.minLevel}+</td>
            <td class="px-6 py-4 text-slate-400">${f.uploadedAt.replace('T', ' ').substring(0, 19)}</td>
            <td class="px-6 py-4 text-right">
              <button onclick="deleteFile('${f.id}')" class="p-1 text-slate-400 hover:text-rose-400"><i class="fa-solid fa-trash-can text-xs"></i></button>
            </td>
          </tr>
        `;
      }).join('');
    }
  } catch (e) {
    console.error('Files error:', e);
  }
}

async function handleFileUpload(e) {
  e.preventDefault();
  const appId = document.getElementById('fileAppId').value;
  const fileInput = document.getElementById('fileUploadInput');
  const minLevel = document.getElementById('fileMinLevel').value;

  if (fileInput.files.length === 0) return;

  const formData = new FormData();
  formData.append('file', fileInput.files[0]);
  formData.append('appId', appId);
  formData.append('minLevel', minLevel);

  try {
    showToast('Uploading payload...', 'info');
    const res = await fetch(`${API_BASE}/files/upload`, {
      method: 'POST',
      headers: { 'X-Admin-Token': adminToken },
      body: formData
    });
    const data = await res.json();
    if (data.success) {
      showToast(`Uploaded ${data.fileName}! File ID: ${data.fileId}`, 'success');
      closeModal('fileModal');
      await loadFiles();
    }
  } catch (e) {
    showToast('Upload failed.', 'error');
  }
}

async function deleteFile(id) {
  if (!confirm('Delete file?')) return;
  try {
    const data = await apiFetch(`${API_BASE}/files/${id}`, { method: 'DELETE' });
    if (data.success) {
      showToast('File removed.', 'info');
      await loadFiles();
    }
  } catch (e) {
    showToast('Failed to delete file.', 'error');
  }
}

// ================= LOGS =================
async function loadLogs() {
  const severity = document.getElementById('logSeverityFilter').value;
  try {
    const data = await apiFetch(`${API_BASE}/logs?appId=${currentAppId}&severity=${severity}`);
    if (data.success) {
      const tbody = document.getElementById('logsTableBody');
      if (data.logs.length === 0) {
        tbody.innerHTML = `<tr><td colspan="6" class="text-center py-8 text-slate-500">No security logs recorded.</td></tr>`;
        return;
      }

      tbody.innerHTML = data.logs.map(l => {
        const sevClass = l.severity === 'DANGER' ? 'text-rose-400 font-bold' : (l.severity === 'WARN' ? 'text-amber-400 font-bold' : 'text-slate-400');
        return `
          <tr class="hover:bg-slate-900/40 transition-colors">
            <td class="px-6 py-3 text-slate-500">${l.timestamp.replace('T', ' ').substring(0, 19)}</td>
            <td class="px-6 py-3 ${sevClass}">[${l.severity}]</td>
            <td class="px-6 py-3 font-bold text-slate-200">${l.action}</td>
            <td class="px-6 py-3 text-purple-400">${l.username || '—'}</td>
            <td class="px-6 py-3 text-slate-400 font-mono">${l.ipAddress || '—'}</td>
            <td class="px-6 py-3 text-slate-300 max-w-sm truncate">${l.details}</td>
          </tr>
        `;
      }).join('');
    }
  } catch (e) {
    console.error('Logs error:', e);
  }
}

async function clearLogs() {
  if (!confirm('Clear all security logs for current application filter?')) return;
  try {
    const data = await apiFetch(`${API_BASE}/logs/clear?appId=${currentAppId}`, { method: 'DELETE' });
    if (data.success) {
      showToast('Logs cleared.', 'info');
      await loadLogs();
    }
  } catch (e) {
    showToast('Failed to clear logs.', 'error');
  }
}

// ================= SETTINGS =================
async function handlePasswordChange(e) {
  e.preventDefault();
  const cur = document.getElementById('currentPass').value;
  const nw = document.getElementById('newPass').value;

  try {
    const data = await apiFetch(`${API_BASE}/change-password`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ currentPassword: cur, newPassword: nw })
    });
    if (data.success) {
      showToast('Master password updated!', 'success');
      document.getElementById('currentPass').value = '';
      document.getElementById('newPass').value = '';
    } else {
      showToast(data.message || 'Error updating password.', 'error');
    }
  } catch (e) {
    showToast('Failed to change password.', 'error');
  }
}

async function testWebhook() {
  const url = document.getElementById('testWebhookUrl').value;
  if (!url) {
    showToast('Please enter a Discord Webhook URL.', 'warning');
    return;
  }
  try {
    const data = await apiFetch(`${API_BASE}/test-webhook`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ url })
    });
    if (data.success) {
      showToast('Test notification embed sent!', 'success');
    }
  } catch (e) {
    showToast('Failed to dispatch webhook alert.', 'error');
  }
}

// ================= MODAL & TOAST HELPERS =================
function openKeyModal() { document.getElementById('keyGenModal').classList.remove('hidden'); }
function openAppModal() { document.getElementById('appModal').classList.remove('hidden'); }
function openVarModal() { document.getElementById('varModal').classList.remove('hidden'); }
function openFileModal() { document.getElementById('fileModal').classList.remove('hidden'); }
function openWalkthroughModal() { document.getElementById('walkthroughModal').classList.remove('hidden'); }
function closeModal(id) { document.getElementById(id).classList.add('hidden'); }

function toggleTourPreference(checked) {
  localStorage.setItem('blackout_hide_tour', checked ? 'true' : 'false');
}

// ================= SDK INTEGRATION CODE GENERATOR =================
let currentSdkLang = 'csharp';

function selectSdkLang(lang) {
  currentSdkLang = lang;
  const btnCs = document.getElementById('sdkBtnCSharp');
  const btnCpp = document.getElementById('sdkBtnCpp');
  const secCs = document.getElementById('sdkSectionCSharp');
  const secCpp = document.getElementById('sdkSectionCpp');

  if (lang === 'csharp') {
    btnCs.className = 'px-5 py-2.5 rounded-xl font-mono text-xs font-bold transition-all bg-purple-600 text-white shadow-lg shadow-purple-600/30 flex items-center gap-2';
    btnCpp.className = 'px-5 py-2.5 rounded-xl font-mono text-xs font-semibold transition-all text-slate-400 hover:text-white bg-[#121522] border border-slate-800 flex items-center gap-2';
    secCs.classList.remove('hidden');
    secCpp.classList.add('hidden');
  } else {
    btnCpp.className = 'px-5 py-2.5 rounded-xl font-mono text-xs font-bold transition-all bg-cyan-600 text-white shadow-lg shadow-cyan-600/30 flex items-center gap-2';
    btnCs.className = 'px-5 py-2.5 rounded-xl font-mono text-xs font-semibold transition-all text-slate-400 hover:text-white bg-[#121522] border border-slate-800 flex items-center gap-2';
    secCpp.classList.remove('hidden');
    secCs.classList.add('hidden');
  }
  updateSdkCodeView();
}

function updateSdkCodeView() {
  const sdkAppSel = document.getElementById('sdkAppSelect');
  if (!sdkAppSel) return;
  const selectedAppId = sdkAppSel.value || (appsCache.length > 0 ? appsCache[0].id : '');
  const app = appsCache.find(a => a.id === selectedAppId) || (appsCache.length > 0 ? appsCache[0] : null);

  const appName = app ? app.name : 'ArenaApp';
  const ownerId = app ? app.ownerId : 'OWNER-001';
  const secret = app ? app.secret : 'arena_secret_9999a8b7c6d5e4f321';
  const version = app ? app.version : '1.0';
  const apiUrl = window.location.origin;

  // C# Snippets
  const csLicense = 
`using System;
using ArenaAuth.SDK.CSharp;

class Program
{
    static void Main()
    {
        // 1. Initialize Arena Client with your app credentials & enable Anti-Debug
        var auth = new ArenaApp(
            name: "${appName}",
            ownerId: "${ownerId}",
            secret: "${secret}",
            version: "${version}",
            apiUrl: "${apiUrl}",
            enableAntiDebug: true // <--- Kills debugger & process hooking tools (x64dbg, dnSpy, CheatEngine)
        );

        // 2. Initialize connection
        if (!auth.Init())
        {
            Console.WriteLine("[-] Handshake / Security Check Failed: " + auth.LastError);
            return;
        }

        Console.Write("Enter your License Key: ");
        string key = Console.ReadLine();

        // 3. Instant Key-Only Authentication (KeyAuth style)
        if (auth.LicenseOnly(key))
        {
            Console.WriteLine($"[+] Access Granted! Welcome {auth.User.Username}");
            Console.WriteLine($"[+] Subscription Expiry: {auth.User.Expiry}");
            Console.WriteLine($"[+] Rank Level: {auth.User.Level}");
            Console.WriteLine($"[+] Hardware ID: {auth.User.Hwid}");

            // 4. UNBYPASSABLE ANTI-CRACK TIP: Fetch critical offsets or strings from cloud
            // If crackers patch the 'LicenseOnly' if-check, this variable won't decrypt!
            string secretKey = auth.GetVar("client_greeting");
            Console.WriteLine("[+] Decrypted Payload Key: " + secretKey);

            // Run protected payload here...
        }
        else
        {
            Console.WriteLine("[-] Access Denied: " + auth.LastError);
        }
    }
}`;

  const csUserPass = 
`using System;
using ArenaAuth.SDK.CSharp;

var auth = new ArenaApp(
    "${appName}", 
    "${ownerId}", 
    "${secret}", 
    "${version}", 
    "${apiUrl}", 
    enableAntiDebug: true
);

if (!auth.Init())
{
    Console.WriteLine("Init failed: " + auth.LastError);
    return;
}

Console.Write("Username: ");
string user = Console.ReadLine();
Console.Write("Password: ");
string pass = Console.ReadLine();

// Method A: Login existing user account
if (auth.Login(user, pass))
{
    Console.WriteLine($"[+] Logged in successfully! Sub Expires: {auth.User.Expiry}");
}

// Method B: Or Register a new user account with a license key
// if (auth.Register(user, pass, "ARENA-LICENSE-KEY")) { ... }`;

  const csCloudVar = 
`// Fetch encrypted cloud variable from server (decrypted locally in memory)
string greeting = auth.GetVar("client_greeting");
Console.WriteLine("Decrypted Cloud String: " + greeting);

// Check session keep-alive status (returns false if revoked)
bool isAlive = auth.Check();

// Download & reflect protected DLL directly in memory (zero disk trace)
byte[] protectedDll = auth.DownloadFile("YOUR_PROTECTED_FILE_ID");

// Send security audit log to dashboard
auth.Log("User initialized protected module.", "INFO");`;

  // C++ Snippets
  const cppLicense = 
`#include <iostream>
#include "ArenaAuth.hpp" // Header-only! Zero dependencies (Native WinHTTP + BCrypt)

int main()
{
    // 1. Initialize Arena Client with your app credentials
    Arena::Auth auth(
        "${appName}",
        "${ownerId}",
        "${secret}",
        "${version}",
        "${apiUrl}"
    );

    // 2. Handshake with server (verifies integrity & checks for debuggers)
    if (!auth.Init())
    {
        std::cout << "[-] Handshake / Security check failed: " << auth.GetLastError() << std::endl;
        return 1;
    }

    std::cout << "Enter License Key: ";
    std::string key;
    std::cin >> key;

    // 3. Instant License-Only Login
    if (auth.LicenseOnly(key))
    {
        std::cout << "[+] Access Granted! Welcome " << auth.User.Username << std::endl;
        std::cout << "[+] Subscription Expiry: " << auth.User.Expiry << std::endl;
        std::cout << "[+] Rank Level: " << auth.User.Level << std::endl;
        std::cout << "[+] Bound HWID: " << auth.User.Hwid << std::endl;

        // 4. ANTI-CRACK TIP: Decrypt critical strings dynamically from cloud
        // Crackers CANNOT patch this because memory decryption requires a valid server key!
        std::string payloadString = auth.GetVar("client_greeting");
        std::cout << "[+] Server-Decrypted Data: " << payloadString << std::endl;

        // Launch your protected C++ application here!
    }
    else
    {
        std::cout << "[-] Access Denied: " << auth.GetLastError() << std::endl;
    }

    return 0;
}`;

  const cppUserPass = 
`#include <iostream>
#include "ArenaAuth.hpp"

Arena::Auth auth("${appName}", "${ownerId}", "${secret}", "${version}", "${apiUrl}");
if (!auth.Init()) return 1;

// Method A: Login with Username & Password
if (auth.Login("user", "password"))
{
    std::cout << "[+] Welcome back " << auth.User.Username << std::endl;
    std::cout << "[+] Subscription Expiry: " << auth.User.Expiry << std::endl;
}

// Method B: Or Register a new user account
// auth.Register("username", "password", "ARENA-LICENSE-KEY");`;

  const cppCloudVar = 
`// Retrieve decrypted cloud variable
std::string secret = auth.GetVar("client_greeting");
std::cout << "[+] Decrypted String: " << secret << std::endl;

// Check session validity & keep alive
bool alive = auth.CheckSession();

// Send event log to dashboard
auth.Log("Client entered main loop", "INFO");`;

  const elCsLic = document.getElementById('csharpCodeLicense');
  const elCsUp = document.getElementById('csharpCodeUserPass');
  const elCsCv = document.getElementById('csharpCodeCloudVar');
  const elCppLic = document.getElementById('cppCodeLicense');
  const elCppUp = document.getElementById('cppCodeUserPass');
  const elCppCv = document.getElementById('cppCodeCloudVar');

  if (elCsLic) elCsLic.textContent = csLicense;
  if (elCsUp) elCsUp.textContent = csUserPass;
  if (elCsCv) elCsCv.textContent = csCloudVar;
  if (elCppLic) elCppLic.textContent = cppLicense;
  if (elCppUp) elCppUp.textContent = cppUserPass;
  if (elCppCv) elCppCv.textContent = cppCloudVar;
}

function copyCode(id) {
  const el = document.getElementById(id);
  if (el) {
    copyToClipboard(el.textContent);
  }
}

function copyToClipboard(text) {
  navigator.clipboard.writeText(text).then(() => {
    showToast('Copied to clipboard!', 'info');
  });
}

function showToast(message, type = 'info') {
  const container = document.getElementById('toastContainer');
  const toast = document.createElement('div');
  const colors = {
    success: 'border-emerald-500/50 bg-[#0c1815]/95 text-emerald-300',
    error: 'border-rose-500/50 bg-[#1a0f12]/95 text-rose-300',
    warning: 'border-amber-500/50 bg-[#1a160d]/95 text-amber-300',
    info: 'border-purple-500/50 bg-[#130f1c]/95 text-purple-300'
  };

  const icons = {
    success: 'fa-circle-check text-emerald-400',
    error: 'fa-circle-exclamation text-rose-400',
    warning: 'fa-triangle-exclamation text-amber-400',
    info: 'fa-circle-info text-purple-400'
  };

  toast.className = `p-4 rounded-xl border shadow-xl flex items-center space-x-3 text-xs font-mono backdrop-blur-md pointer-events-auto transition-all duration-300 opacity-0 translate-y-2 ${colors[type] || colors.info}`;
  toast.innerHTML = `<i class="fa-solid ${icons[type] || icons.info}"></i> <span>${message}</span>`;

  container.appendChild(toast);
  requestAnimationFrame(() => {
    toast.classList.remove('opacity-0', 'translate-y-2');
  });

  setTimeout(() => {
    toast.classList.add('opacity-0', 'translate-y-2');
    setTimeout(() => toast.remove(), 300);
  }, 3500);
}
