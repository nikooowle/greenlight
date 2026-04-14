const http = require('http');
const fs = require('fs');
const path = require('path');

const types = { '.html': 'text/html', '.json': 'application/json', '.js': 'application/javascript', '.css': 'text/css' };

http.createServer((req, res) => {
  let url = req.url === '/' ? '/index.html' : req.url;
  const filePath = path.join(__dirname, url);
  fs.readFile(filePath, (err, data) => {
    if (err) {
      res.writeHead(404);
      res.end('Not found');
    } else {
      const ext = path.extname(filePath);
      res.writeHead(200, { 'Content-Type': types[ext] || 'text/plain' });
      res.end(data);
    }
  });
}).listen(4100, () => console.log('Matrix view listening on http://localhost:4100'));
