import { useEffect, useRef } from 'react';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';

// Map centre used until there is something to show (the middle of Sri Lanka).
const DEFAULT_CENTER = [7.8731, 80.7718];

/** Builds popup content with textContent so text from the database can never be interpreted as HTML. */
function popupNode(lines) {
  const box = document.createElement('div');
  lines.filter(Boolean).forEach((line, index) => {
    const row = document.createElement(index === 0 ? 'strong' : 'div');
    row.textContent = line;
    box.appendChild(row);
  });
  return box;
}

/**
 * Interactive map (Leaflet + OpenStreetMap tiles).
 * points: [{ id, lat, lng, color, radius?, lines: ['Title', 'detail', ...] }]
 * Circle markers are used instead of pin images so nothing extra has to be bundled. If the tiles cannot load (offline)
 * the markers still show on a plain background.
 */
export default function MapView({ points, height = 380, label = 'Map' }) {
  const containerRef = useRef(null);
  const mapRef = useRef(null);
  const layerRef = useRef(null);

  useEffect(() => {
    const map = L.map(containerRef.current, { scrollWheelZoom: false }).setView(DEFAULT_CENTER, 7);
    L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
      maxZoom: 19,
      attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
    }).addTo(map);
    layerRef.current = L.layerGroup().addTo(map);
    mapRef.current = map;
    return () => {
      map.remove();
      mapRef.current = null;
      layerRef.current = null;
    };
  }, []);

  useEffect(() => {
    const map = mapRef.current;
    const layer = layerRef.current;
    if (!map || !layer) return;
    layer.clearLayers();
    const bounds = [];
    (points ?? []).forEach((p) => {
      if (typeof p.lat !== 'number' || typeof p.lng !== 'number') return;
      L.circleMarker([p.lat, p.lng], {
        radius: p.radius ?? 9,
        color: '#ffffff',
        weight: 2,
        fillColor: p.color ?? '#1a5fd0',
        fillOpacity: 0.9
      }).bindPopup(popupNode(p.lines ?? [])).addTo(layer);
      bounds.push([p.lat, p.lng]);
    });
    if (bounds.length === 1) map.setView(bounds[0], 13);
    else if (bounds.length > 1) map.fitBounds(bounds, { padding: [30, 30], maxZoom: 14 });
  }, [points]);

  return <div ref={containerRef} className="map" style={{ height }} role="region" aria-label={label} />;
}
