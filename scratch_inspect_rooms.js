const fs = require('fs');
const path = require('path');

const roomNames = [
    'B1', 'B2', 'B3', 'B4_Corridor', 'B5', 'B6_Corridor', 'B7_Crossroad',
    'S2_Safe', 'S3', 'S4', 'S5', 'Start', 'Van_Room'
];

console.log('Room'.padEnd(16) + ' | Sockets | ' + 'Socket Names'.padEnd(45) + ' | BoxCollider Size');
console.log('-'.repeat(100));

for (const r of roomNames) {
    let p = path.join('Assets', 'Rooms', `${r}.prefab`);
    if (!fs.existsSync(p)) {
        p = path.join('Assets', `${r}.prefab`);
    }
    if (!fs.existsSync(p)) {
        console.log(r.padEnd(16) + ' | NOT FOUND');
        continue;
    }

    const content = fs.readFileSync(p, 'utf-8');
    const socketMatches = [...content.matchAll(/m_Name:\s*(Socket[^\r\n]*)/g)].map(m => m[1].trim());

    // Box collider on root or children
    const boxMatch = content.match(/BoxCollider:[\s\S]*?m_Size:\s*\{x:\s*([^,]+),\s*y:\s*([^,]+),\s*z:\s*([^}]+)\}/);
    const boxSize = boxMatch ? `(${boxMatch[1]}, ${boxMatch[2]}, ${boxMatch[3]})` : 'NONE';

    console.log(r.padEnd(16) + ' | ' + String(socketMatches.length).padEnd(7) + ' | ' + socketMatches.join(', ').padEnd(45) + ' | ' + boxSize);
}
