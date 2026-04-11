'use strict';

const net = require('net');

function tcpPing(port, timeoutMs = 1000) {
  return new Promise(resolve => {
    const sock = new net.Socket();
    let done = false;
    const finish = ok => {
      if (done) return;
      done = true;
      sock.destroy();
      resolve(ok);
    };
    sock.setTimeout(timeoutMs);
    sock.once('connect', () => finish(true));
    sock.once('error', () => finish(false));
    sock.once('timeout', () => finish(false));
    sock.connect(port, '127.0.0.1');
  });
}

// length-prefixed JSON 요청 전송 후 응답 파싱
// 요청: { id, tool, params }
// 응답: { id, success, data, error }
function sendRequest(port, tool, params = {}, timeoutMs = 3000) {
  return new Promise((resolve, reject) => {
    const sock = new net.Socket();
    let done = false;
    const finish = (err, result) => {
      if (done) return;
      done = true;
      sock.destroy();
      if (err) reject(err); else resolve(result);
    };

    sock.setTimeout(timeoutMs);
    sock.once('timeout', () => finish(new Error('timeout')));
    sock.once('error', err => finish(err));

    let buf = Buffer.alloc(0);
    let expected = null;

    sock.on('data', chunk => {
      buf = Buffer.concat([buf, chunk]);
      if (expected === null && buf.length >= 4) {
        expected = buf.readUInt32BE(0);
        buf = buf.slice(4);
      }
      if (expected !== null && buf.length >= expected) {
        const payload = buf.slice(0, expected).toString('utf8');
        try {
          const parsed = JSON.parse(payload);
          if (parsed.success) finish(null, parsed.data);
          else finish(new Error(parsed.error || 'unknown error'));
        } catch (e) {
          finish(e);
        }
      }
    });

    sock.once('connect', () => {
      const body = JSON.stringify({
        id: Date.now().toString(),
        tool,
        params,
      });
      const bodyBuf = Buffer.from(body, 'utf8');
      const lenBuf = Buffer.alloc(4);
      lenBuf.writeUInt32BE(bodyBuf.length, 0);
      sock.write(Buffer.concat([lenBuf, bodyBuf]));
    });

    sock.connect(port, '127.0.0.1');
  });
}

async function getEditorState(port) {
  return sendRequest(port, 'unity_get_editor_state', {}, 2000);
}

module.exports = { tcpPing, sendRequest, getEditorState };
