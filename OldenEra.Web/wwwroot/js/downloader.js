// Triggers a browser download from a base64-encoded byte payload.
window.oeDownloader = {
    download: function (filename, mimeType, base64) {
        try {
            const binary = atob(base64);
            const bytes = new Uint8Array(binary.length);
            for (let i = 0; i < binary.length; i++) {
                bytes[i] = binary.charCodeAt(i);
            }
            const blob = new Blob([bytes], { type: mimeType || 'application/octet-stream' });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = filename;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            // Defer revocation so the click has a chance to consume the URL.
            setTimeout(() => URL.revokeObjectURL(url), 1500);
            return true;
        } catch (e) {
            console.error('oeDownloader failed', e);
            return false;
        }
    }
};
