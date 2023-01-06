function getLastUsedFolder() {
    return localStorage.getItem("lastUsedFolder");
}

function setLastUsedFolder(value) {
    return localStorage.setItem("lastUsedFolder", value);
}

function getLastUsedTargetFolder() {
    return localStorage.getItem("lastUsedTargetFolder");
}

function setLastUsedTargetFolder(value) {
    return localStorage.setItem("lastUsedTargetFolder", value);
}