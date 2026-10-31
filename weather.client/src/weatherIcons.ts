const icons: Record<string, string> = {
    clear: '☀️',
    'clear-night': '🌙',
    'mostly-clear': '🌤️',
    'mostly-clear-night': '🌙',
    'partly-cloudy': '⛅',
    'partly-cloudy-night': '☁️',
    cloudy: '☁️',
    fog: '🌫️',
    drizzle: '🌦️',
    rain: '🌧️',
    sleet: '🌨️',
    snow: '❄️',
    storm: '⛈️',
};

export function weatherIcon(key: string): string {
    return icons[key] ?? '❔';
}
