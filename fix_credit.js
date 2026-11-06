const fs = require('fs');
const path = require('path');
const file = path.join('BlackoutAuth.Server', 'wwwroot', 'js', 'dashboard.js');
let content = fs.readFileSync(file, 'utf8');

// Pattern to remove the anti-skid interval
const targetRegex = /\/\/ ANTI-SKID[\s\S]*?setInterval\(\(\) => \{[\s\S]*?\}, 2000\);/g;

if (targetRegex.test(content)) {
  content = content.replace(targetRegex, '// Developer credit watermark check removed');
  fs.writeFileSync(file, content, 'utf8');
  console.log('Anti-skid check successfully removed from dashboard.js');
} else {
  console.log('Target pattern not found.');
}
