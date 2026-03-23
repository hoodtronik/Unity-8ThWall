var XR8SessionRecorderLib = {

    WebGLDownloadCSV: function(filenamePtr, csvContentPtr) {
        var filename = UTF8ToString(filenamePtr);
        var csvContent = UTF8ToString(csvContentPtr);

        // Create blob and trigger browser download
        var blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
        var url = URL.createObjectURL(blob);

        var link = document.createElement('a');
        link.setAttribute('href', url);
        link.setAttribute('download', filename);
        link.style.visibility = 'hidden';

        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);

        // Cleanup
        setTimeout(function() { URL.revokeObjectURL(url); }, 1000);

        console.log('[XR8SessionRecorder] Downloaded: ' + filename);
    }
};

mergeInto(LibraryManager.library, XR8SessionRecorderLib);
