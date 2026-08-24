const isNexaProduction = typeof window !== "undefined"
  && (window.location.hostname === "nexacoding.website" || window.location.hostname === "www.nexacoding.website");

export const API_URL = import.meta.env.VITE_API_URL
  ?? (isNexaProduction ? "https://nexacode-g8dj.onrender.com/api" : "/api");

export const COLLABORATION_HUB_URL = import.meta.env.VITE_SIGNALR_URL
  ?? (isNexaProduction ? "https://nexacode-g8dj.onrender.com/hubs/collaboration" : "/hubs/collaboration");
