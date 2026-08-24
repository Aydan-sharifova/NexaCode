const productionApiUrl = "https://nexacode-g8dj.onrender.com/api";
const productionHubUrl = "https://nexacode-g8dj.onrender.com/hubs/collaboration";

export const API_URL = import.meta.env.VITE_API_URL
  ?? (import.meta.env.PROD ? productionApiUrl : "/api");

export const COLLABORATION_HUB_URL = import.meta.env.VITE_SIGNALR_URL
  ?? (import.meta.env.PROD ? productionHubUrl : "/hubs/collaboration");
