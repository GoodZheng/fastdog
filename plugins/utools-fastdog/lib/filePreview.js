// 移植自 src/FastDog/Services/FilePreviewService.cs。
// BinaryExtensions / MaxFileSize(5MB) / MaxLines(5000) / ByteToCharOffset 语义保持一致。

const fs = require('node:fs');
const path = require('node:path');

const MAX_FILE_SIZE = 5 * 1024 * 1024; // 5MB
const MAX_LINES = 5000;

const BINARY_EXTENSIONS = new Set([
  '.exe', '.dll', '.pdb', '.obj', '.o', '.so', '.dylib',
  '.png', '.jpg', '.jpeg', '.gif', '.bmp', '.ico', '.tif', '.tiff', '.webp',
  '.zip', '.rar', '.7z', '.tar', '.gz', '.bz2', '.xz',
  '.mp3', '.mp4', '.avi', '.mkv', '.mov', '.wmv', '.flac', '.wav',
  '.pdf', '.doc', '.docx', '.xls', '.xlsx', '.ppt', '.pptx',
  '.bin', '.dat', '.db', '.sqlite', '.mdb',
  '.class', '.jar', '.war', '.nupkg', '.snk',
  '.woff', '.woff2', '.ttf', '.eot',
]);

/**
 * 按扩展名判断是否二进制（移植自 IsBinaryFile，大小写不敏感）。
 */
function isBinaryFile(filePath) {
  const ext = path.extname(filePath).toLowerCase();
  return BINARY_EXTENSIONS.has(ext);
}

/**
 * 加载文件内容。
 * @returns {{content: string|null, truncated: boolean, isBinary: boolean, lineLengths: number[]}}
 */
function loadFileContent(filePath) {
  const result = { content: null, truncated: false, isBinary: false, lineLengths: [] };
  try {
    if (!fs.existsSync(filePath)) return result;

    if (isBinaryFile(filePath)) {
      result.isBinary = true;
      return result;
    }

    const size = fs.statSync(filePath).size;
    let content;
    if (size > MAX_FILE_SIZE) {
      result.truncated = true;
      content = readFirstLines(filePath, MAX_LINES);
    } else {
      content = fs.readFileSync(filePath, 'utf8');
    }
    result.content = content;
    result.lineLengths = computeLineLengths(content);
  } catch (e) {
    // IOException / UnauthorizedAccessException 对应：读失败返回 null content
    result.content = null;
  }
  return result;
}

function readFirstLines(filePath, maxLines) {
  const lines = [];
  const data = fs.readFileSync(filePath, 'utf8');
  const all = data.split('\n');
  for (let i = 0; i < maxLines && i < all.length; i++) lines.push(all[i]);
  return lines.join('\n') + '\n';
}

/**
 * 计算每行长度（含 \n），用于把行内偏移转全局偏移。移植自 ComputeLineLengths。
 */
function computeLineLengths(content) {
  const lines = content.split('\n');
  let count = lines.length;
  if (count > 0 && lines[count - 1].length === 0) count--;
  const lengths = new Array(count);
  for (let i = 0; i < count; i++) {
    lengths[i] = lines[i].length + (i < count - 1 ? 1 : 0); // 含 \n（最后一行除外）
  }
  return lengths;
}

/**
 * 将 UTF-8 字节偏移转换为字符串字符偏移（移植自 ByteToCharOffset）。
 * ASCII 两者相同；中文等多字节字符字节偏移 > 字符偏移。
 */
function byteToCharOffset(text, byteOffset) {
  if (byteOffset <= 0) return 0;
  if (!text) return byteOffset;
  const bytes = new TextEncoder().encode(text);
  if (byteOffset >= bytes.length) return text.length;
  return new TextDecoder('utf8').decode(bytes.slice(0, byteOffset)).length;
}

module.exports = { isBinaryFile, loadFileContent, byteToCharOffset, computeLineLengths };
