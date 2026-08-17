(function () {
    'use strict';

    // ---------- Navbar scroll state ----------
    var navbar = document.getElementById('navbar');
    function onScroll() {
        if (window.scrollY > 12) navbar.classList.add('scrolled');
        else navbar.classList.remove('scrolled');
    }
    window.addEventListener('scroll', onScroll, { passive: true });
    onScroll();

    // ---------- Mobile menu ----------
    var hamburger = document.getElementById('hamburger');
    var mobileMenu = document.getElementById('mobileMenu');
    hamburger.addEventListener('click', function () {
        hamburger.classList.toggle('open');
        mobileMenu.classList.toggle('open');
    });
    mobileMenu.querySelectorAll('a').forEach(function (link) {
        link.addEventListener('click', function () {
            hamburger.classList.remove('open');
            mobileMenu.classList.remove('open');
        });
    });

    // ---------- Scroll reveal ----------
    var revealObserver = new IntersectionObserver(function (entries) {
        entries.forEach(function (entry) {
            if (entry.isIntersecting) {
                entry.target.classList.add('visible');
                revealObserver.unobserve(entry.target);
            }
        });
    }, { threshold: 0.12 });
    document.querySelectorAll('.reveal').forEach(function (el) { revealObserver.observe(el); });

    // ---------- Card cursor glow ----------
    document.querySelectorAll('.card').forEach(function (card) {
        card.addEventListener('mousemove', function (e) {
            var rect = card.getBoundingClientRect();
            card.style.setProperty('--mx', ((e.clientX - rect.left) / rect.width * 100) + '%');
        });
    });

    // ---------- Counters ----------
    var counters = document.querySelectorAll('.counter');
    function animateCounter(el) {
        var target = parseInt(el.dataset.target, 10) || 0;
        var duration = 1400;
        var start = null;
        function tick(ts) {
            if (!start) start = ts;
            var progress = Math.min((ts - start) / duration, 1);
            var eased = 1 - Math.pow(1 - progress, 3);
            el.textContent = Math.round(target * eased);
            if (progress < 1) requestAnimationFrame(tick);
        }
        requestAnimationFrame(tick);
    }
    var counterObserver = new IntersectionObserver(function (entries) {
        entries.forEach(function (entry) {
            if (entry.isIntersecting) {
                animateCounter(entry.target);
                counterObserver.unobserve(entry.target);
            }
        });
    }, { threshold: 0.4 });
    counters.forEach(function (el) { counterObserver.observe(el); });

    // ---------- FAQ accordion ----------
    document.querySelectorAll('.faq-item').forEach(function (item) {
        var q = item.querySelector('.faq-q');
        var a = item.querySelector('.faq-a');
        q.addEventListener('click', function () {
            var isOpen = item.classList.contains('open');
            document.querySelectorAll('.faq-item.open').forEach(function (other) {
                other.classList.remove('open');
                other.querySelector('.faq-a').style.maxHeight = null;
            });
            if (!isOpen) {
                item.classList.add('open');
                a.style.maxHeight = a.scrollHeight + 'px';
            }
        });
    });

    // ---------- Phone tilt ----------
    var phone = document.getElementById('phone');
    if (phone && window.matchMedia('(hover: hover)').matches) {
        var heroVisual = phone.parentElement;
        heroVisual.addEventListener('mousemove', function (e) {
            var rect = heroVisual.getBoundingClientRect();
            var px = (e.clientX - rect.left) / rect.width - 0.5;
            var py = (e.clientY - rect.top) / rect.height - 0.5;
            phone.style.transform = 'perspective(1000px) rotateY(' + (px * 7) + 'deg) rotateX(' + (-py * 7) + 'deg)';
        });
        heroVisual.addEventListener('mouseleave', function () {
            phone.style.transform = '';
        });
    }

    // ---------- Footer year ----------
    var year = document.getElementById('year');
    if (year) year.textContent = new Date().getFullYear();
})();