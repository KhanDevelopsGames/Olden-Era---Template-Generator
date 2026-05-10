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

window.oeShare = {
    getHash: function () {
        return (window.location.hash || "").replace(/^#/, "");
    },
    setHash: function (value) {
        const url = new URL(window.location.href);
        url.hash = value ? "#" + value : "";
        history.replaceState(null, "", url.toString());
    },
    buildShareUrl: function (encoded) {
        const url = new URL(window.location.href);
        url.hash = "#s=" + encoded;
        return url.toString();
    },
    copy: async function (text) {
        if (navigator.clipboard && navigator.clipboard.writeText) {
            await navigator.clipboard.writeText(text);
            return true;
        }
        const ta = document.createElement("textarea");
        ta.value = text;
        ta.style.position = "fixed";
        ta.style.opacity = "0";
        document.body.appendChild(ta);
        ta.select();
        let ok = false;
        try { ok = document.execCommand("copy"); } catch { ok = false; }
        document.body.removeChild(ta);
        return ok;
    },
};
