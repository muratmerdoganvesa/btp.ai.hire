"use strict";

const fs = require("fs");
const path = require("path");
const approuter = require("@sap/approuter");

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

function pathnameOf(req) {
  const raw = String(req._hirelensUrl || req.url || "").split("?")[0];
  try {
    return decodeURIComponent(raw);
  } catch {
    return raw;
  }
}

function isProxyPath(pathname) {
  return (
    pathname.startsWith("/api/") ||
    pathname.startsWith("/compliance/") ||
    pathname.startsWith("/health/") ||
    pathname.startsWith("/login/callback") ||
    pathname === "/logout" ||
    pathname.startsWith("/logout/")
  );
}

function resolveUiFile(pathname) {
  if (pathname === "/" || pathname === "" || pathname === "/index.html") {
    return { file: indexHtml, missingAsset: false };
  }
  const exact = findFile(pathname.replace(/^\/+/, ""));
  if (exact) {
    return { file: exact, missingAsset: false };
  }
  const ext = path.extname(pathname);
  if (ext && ext !== ".html") {
    return { file: null, missingAsset: true };
  }
  return { file: indexHtml, missingAsset: false };
}

function serveUi(req, res) {
  const pathname = pathnameOf(req);
  if (pathname === "/login" || pathname === "/login/") {
    res.writeHead(302, { Location: "/" });
    res.end();
    return;
  }

  const { file, missingAsset } = resolveUiFile(pathname);
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
}

const ar = approuter();
ar.start({
  workingDir: __dirname,
  extensions: [
    {
      insertMiddleware: {
        first: [
          {
            handler(req, res, next) {
              req._hirelensUrl = req.url;
              next();
            }
          }
        ],
        beforeRequestHandler: [
          {
            handler(req, res, next) {
              if (req.method !== "GET" && req.method !== "HEAD") {
                return next();
              }
              if (isProxyPath(pathnameOf(req))) {
                return next();
              }
              return serveUi(req, res);
            }
          }
        ],
        beforeErrorHandler: [
          {
            handler(err, req, res, next) {
              const status = err && (err.status || err.statusCode);
              if (
                err &&
                status === 404 &&
                (req.method === "GET" || req.method === "HEAD") &&
                !isProxyPath(pathnameOf(req))
              ) {
                return serveUi(req, res);
              }
              return next(err);
            }
          }
        ]
      }
    }
  ]
});
