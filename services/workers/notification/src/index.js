import pino from "pino";
import dotenv from "dotenv";
import http from "http";
dotenv.config();
const logger = pino();
logger.info("Notification Service Skeleton Initialized");
// Global error handlers
process.on("uncaughtException", (err) => {
    logger.fatal({ err }, "Uncaught Exception");
    process.exit(1);
});
process.on("unhandledRejection", (reason, promise) => {
    logger.fatal({ reason, promise }, "Unhandled Rejection");
    process.exit(1);
});
// Graceful shutdown
const shutdown = () => {
    logger.info("Shutting down gracefully...");
    process.exit(0);
};
process.on("SIGINT", shutdown);
process.on("SIGTERM", shutdown);
// Health Endpoint
const server = http.createServer((req, res) => {
    if (req.url === "/health" && req.method === "GET") {
        res.writeHead(200, { "Content-Type": "application/json" });
        res.end(JSON.stringify({ status: "healthy" }));
    }
    else {
        res.writeHead(404);
        res.end();
    }
});
const PORT = process.env.PORT || 3000;
server.listen(PORT, () => {
    logger.info(`Notification Service running on port ${PORT}`);
});
//# sourceMappingURL=index.js.map