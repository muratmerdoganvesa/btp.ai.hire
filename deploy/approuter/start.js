"use strict";

const fs = require("fs");
const path = require("path");
const approuter = require("@sap/approuter");

const ar = approuter();
const roots = [
  path.join(__dirname, "resources"),
  __dirname
];

const mime = {
  ".html": "text/html; charset=utf-8",
  ".js": "application/javascript; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".svg": "image/svg+xml",
  ".png": "image/png",
  ".jpg": "image/jpeg",
  ".jpeg": "image/jpeg",
  ".ico": "image/x-icon",
  ".woff": "font/woff",
  ".woff2": "font/woff2",
  ".map": "application/json"
};

function pathnameOf(req) {
  const raw = String(req._hirelensUrl || req.url || "").split("?")[0];
  try {
    return decodeURIComponent(raw);
  } catch {
    return raw;
  }
}

function isApprouterPath(pathname) {
  return (
    pathname.startsWith("/api/") ||
    pathname.startsWith("/compliance/") ||
    pathname.startsWith("/health/") ||
    pathname.startsWith("/login") ||
    pathname === "/logout" ||
    pathname.startsWith("/logout/")
  );
}

function findFile(rel) {
  const clean = rel.replace(/^\/+/, "").replace(/\\/g, "/");
  if (clean.includes("..")) {
    return null;
  }
  for (const root of roots) {
    const candidate = path.resolve(root, clean);
    if (!candidate.startsWith(path.resolve(root))) {
      continue;
    }
    if (fs.existsSync(candidate) && fs.statSync(candidate).isFile()) {
      return candidate;
    }
  }
  return null;
}

function sendFile(res, file) {
  res.statusCode = 200;
  res.setHeader("Content-Type", mime[path.extname(file).toLowerCase()] || "application/octet-stream");
  res.setHeader("Cache-Control", "no-cache, no-store, must-revalidate");
  fs.createReadStream(file).pipe(res);
}

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
              const pathname = pathnameOf(req);
              if (isApprouterPath(pathname)) {
                return next();
              }

              const rel =
                pathname === "/" || pathname === ""
                  ? "index.html"
                  : pathname.replace(/^\/+/, "");
              const file = findFile(rel) || findFile("index.html");
              if (!file) {
                res.statusCode = 500;
                res.setHeader("Content-Type", "text/plain; charset=utf-8");
                return res.end("HireLens UI missing. Looked for index.html under resources/ and app root.");
              }
              if (req.method === "HEAD") {
                res.statusCode = 200;
                res.setHeader("Content-Type", mime[path.extname(file).toLowerCase()] || "application/octet-stream");
                return res.end();
              }
              return sendFile(res, file);
            }
          }
        ]
      }
    }
  ]
});
