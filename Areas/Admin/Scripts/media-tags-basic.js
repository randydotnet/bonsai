﻿$(function() {
    var $wrap = $('.media-editor-tags-wrapper');
    if ($wrap.length === 0) {
        return;
    }

    var $entitiesField = $('#DepictedEntities');

    var eventType = [2],
        locationType = [3],
        otherTypes = [0, 1, 4];

    setupSelectize($('#Location'), locationType);
    setupSelectize($('#Event'), eventType);
    setupSelectize($('#media-editor-tags-list'), otherTypes, function ($s) {
        var sel = $s[0].selectize;
        var result = sel.getValue().map(function(v) {
            return {
                PageId: v,
                ObjectTitle: sel.getItem(v).text()
            };
        });
        var data = JSON.stringify(result);
        $entitiesField.val(data);
    });

    function setupSelectize($select, types, handler) {
        var multiple = $select.prop('multiple');
        $select.selectize({
            create: true,
            maxOptions: 10,
            maxItems: multiple ? null : 1,
            openOnFocus: true,
            valueField: 'id',
            labelField: 'title',
            sortField: 'title',
            searchField: 'title',
            placeholder: 'Страница или название',
            preload: true,
            load: function (query, callback) {
                loadData(query, types, callback);
            },
            onChange: function () {
                if (!!handler) {
                    handler($select);
                }
            },
            render: {
                option_create: function(data, escape) {
                    return '<div class="create">' + escape(data.input) + ' <i>(без ссылки)</i></div>';
                }
            }
        });
    }

    function loadData(query, types, callback) {
        // loads data according to current query
        var url = '/admin/suggest/pages?query=' + encodeURIComponent(query);
        types.forEach(function (t) { url += '&types=' + encodeURIComponent(t); });

        $.ajax(url)
            .done(function (data) {
                callback(data);
            });
    }
});