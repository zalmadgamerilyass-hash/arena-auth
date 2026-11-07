const https = require('https');
const url = 'https://arena-auth-production.up.railway.app';
https.get(url, (res) => {
  let data = '';
  res.on('data', chunk => data += chunk);
  res.on('end', () => {
    console.log('Headers:', res.headers);
    const lines = data.split('\n');
    lines.slice(0, 30).forEach((l, i) => console.log(i + 1, l));
  });
}).on('error', (e) => console.log('Error:', e.message));
