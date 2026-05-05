const http = require('http');
const fs = require('fs');
const path = require('path');

function getContentType(filePath) {
    const ext = path.extname(filePath).toLowerCase();
    if (ext === '.html') return 'text/html; charset=utf-8';
    if (ext === '.js') return 'application/javascript; charset=utf-8';
    if (ext === '.css') return 'text/css; charset=utf-8';
    if (ext === '.json') return 'application/json; charset=utf-8';
    if (ext === '.png') return 'image/png';
    if (ext === '.jpg' || ext === '.jpeg') return 'image/jpeg';
    if (ext === '.webp') return 'image/webp';
    return 'application/octet-stream';
}

const server = http.createServer((req, res) => {
    const requestPath = req.url === '/' ? 'index.html' : req.url;
    const cleanPath = requestPath.split('?')[0];
    let filePath = path.join(__dirname, cleanPath);
    fs.readFile(filePath, (err, data) => {
        if (err) {
            res.writeHead(404);
            res.end('File not found');
            return;
        }
        res.writeHead(200, {
            'Content-Type': getContentType(filePath),
            'Cache-Control': 'no-store, no-cache, must-revalidate, proxy-revalidate',
            Pragma: 'no-cache',
            Expires: '0'
        });
        res.end(data);
    });
});

server.listen(8000, () => {
    console.log('Server running at http://localhost:8000');
});