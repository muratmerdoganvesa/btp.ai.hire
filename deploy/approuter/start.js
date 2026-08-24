"use strict";

const fs = require("fs");
const http = require("http");
const path = require("path");
const approuter = require("@sap/approuter");

const UI_HOST = "127.0.0.1";
const UI_PORT = Number(process.env.HIRELENS_UI_PORT || 8099);
const roots = [path.join(__dirname, "resources"), __dirname];

const mime = {
  ".css": "text/css; charset=utf-8",
  ".html": "text/html; charset=utf-8",
  ".ico": "image/x-icon",
  ".jpeg": "image/jpeg",
  ".jpg": "image/jpeg",
  ".js": "application/javascript; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".map": "application/json",
  ".mjs": "application/javascript; charset=utf-8",
  ".png": "image/png",
  ".svg": "image/svg+xml",
  ".txt": "text/plain; charset=utf-8",
  ".webp": "image/webp",
  ".woff": "font/woff",
  ".woff2": "font/woff2"
};

function findFile(rel) {
  const clean = String(rel || "")
    .replace(/^\/+/, "")
    .replace(/\\/g, "/");
  if (!clean || clean.includes("..")) {
    return null;
  }
  for (const root of roots) {
    const base = path.resolve(root);
    const candidate = path.resolve(root, clean);
    if (!candidate.startsWith(base + path.sep) && candidate !== base) {
      continue;
    }
    try {
      if (fs.statSync(candidate).isFile()) {
        return candidate;
      }
    } catch {
      /* missing */
    }
  }
  return null;
}

const indexHtml = findFile("index.html");
if (!indexHtml) {
  console.error("FATAL: React index.html missing. cwd=", process.cwd(), "dir=", __dirname);
  try {
    console.error("root listing:", fs.readdirSync(__dirname).join(", "));
  } catch {
    /* ignore */
  }
  process.exit(1);
}
console.log("HireLens UI index.html:", indexHtml);

function sendFile(res, file) {
  const type = mime[path.extname(file).toLowerCase()] || "application/octet-stream";
  res.statusCode = 200;
  res.setHeader("Content-Type", type);
  res.setHeader("Cache-Control", file === indexHtml ? "no-cache, no-store, must-revalidate" : "public, max-age=31536000, immutable");
  fs.createReadStream(file).pipe(res);
}

function pathnameOf(url) {
  const raw = String(url || "/").split("?")[0];
  try {
    return decodeURIComponent(raw);
  } catch {
    return raw;
  }
}

function resolveUiFile(pathname) {
  if (pathname === "/" || pathname === "" || pathname === "/index.html") {
    return { file: indexHtml, missingAsset: false };
  }
  const rel = pathname.replace(/^\/+/, "");
  const exact = findFile(rel);
  if (exact) {
    return { file: exact, missingAsset: false };
  }
  const ext = path.extname(pathname);
  if (ext && ext !== ".html") {
    return { file: null, missingAsset: true };
  }
  return { file: indexHtml, missingAsset: false };
}

const uiServer = http.createServer((req, res) => {
  if (req.method !== "GET" && req.method !== "HEAD") {
    res.statusCode = 405;
    res.end();
    return;
  }
  const { file, missingAsset } = resolveUiFile(pathnameOf(req.url));
  if (missingAsset || !file) {
    res.statusCode = 404;
    res.setHeader("Content-Type", "text/plain; charset=utf-8");
    res.end("Not Found");
    return;
  }
  if (req.method === "HEAD") {
    res.statusCode = 200;
    res.setHeader("Content-Type", mime[path.extname(file).toLowerCase()] || "application/octet-stream");
    res.end();
    return;
  }
  sendFile(res, file);
});

uiServer.on("error", (err) => {
  console.error("FATAL: static UI server failed", err);
  process.exit(1);
});

uiServer.listen(UI_PORT, UI_HOST, () => {
  const destinations = JSON.parse(process.env.destinations || "[]");
  if (!destinations.some((item) => item && item.name === "hirelens-ui")) {
    destinations.push({
      name: "hirelens-ui",
      url: `http://${UI_HOST}:${UI_PORT}`,
      forwardAuthToken: false
    });
  }
  process.env.destinations = JSON.stringify(destinations);
  console.log("HireLens static UI:", `http://${UI_HOST}:${UI_PORT}`);

  approuter().start({ workingDir: __dirname });
});
