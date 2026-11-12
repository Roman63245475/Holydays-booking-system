const storage = {
    set: (key: string, value:object) => {
        localStorage.setItem(key, JSON.stringify(value));
    },
    get: (key: string) => {
        return localStorage.getItem(key);
    },
    remove: (key: string) => {
        localStorage.removeItem(key);
    },
    clear: () => {
        localStorage.clear();
    }
}

export default storage;