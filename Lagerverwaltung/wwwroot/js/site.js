document.getElementById('themeToggle')?.addEventListener('click', () => {
    const neu = document.documentElement.getAttribute('data-bs-theme') === 'dark' ? 'light' : 'dark';
    document.documentElement.setAttribute('data-bs-theme', neu);
    localStorage.setItem('theme', neu);
});