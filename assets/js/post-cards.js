---
layout: null
---
/* Shared client-side post-card rendering.
   Used by the search page and the tags page so both build identical cards
   (and identical tag links) from `search.json`. */
(function (global) {
    var TAGS_URL = '{{ "/tags/" | relative_url }}';

    function escapeHtml(s) {
        return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
    }

    function escapeRegex(s) {
        return s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    }

    function highlight(text, terms) {
        if (!terms || !terms.length) return escapeHtml(text);
        var pattern = new RegExp('(' + terms.map(escapeRegex).join('|') + ')', 'gi');
        return escapeHtml(text).replace(pattern, '<mark>$1</mark>');
    }

    function tagUrl(tag) {
        return TAGS_URL + '?tag=' + encodeURIComponent(tag);
    }

    function tagsHtml(tags, activeTag) {
        if (!tags || !tags.length) return '';
        var active = (activeTag || '').toLowerCase();
        return '<div class="post-tags" aria-label="Tags">' +
            tags.map(function (t) {
                var current = t.toLowerCase() === active ? ' tag-current" aria-current="page' : '';
                return '<a class="tag' + current + '" href="' + escapeHtml(tagUrl(t)) + '">#' + escapeHtml(t) + '</a>';
            }).join('') +
            '</div>';
    }

    function render(post, terms, activeTag) {
        return '<article class="post-card">' +
            '<div class="post-body">' +
                '<div class="post-meta"><time>' + escapeHtml(post.date) + '</time></div>' +
                '<h2 class="post-title"><a href="' + escapeHtml(post.url) + '">' + highlight(post.title, terms) + '</a></h2>' +
                '<p class="post-excerpt">' + highlight(post.excerpt, terms) + '</p>' +
                tagsHtml(post.tags, activeTag) +
                '<p><a class="read-more" href="' + escapeHtml(post.url) + '">Read More &rarr;</a></p>' +
            '</div>' +
        '</article>';
    }

    global.PostCards = {
        escapeHtml: escapeHtml,
        escapeRegex: escapeRegex,
        highlight: highlight,
        tagUrl: tagUrl,
        render: render
    };
})(window);
