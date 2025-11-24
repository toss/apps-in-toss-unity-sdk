mergeInto(LibraryManager.library, {
    SaveToLocalStorage: function (key, value) {
        var keyStr = UTF8ToString(key);
        var valueStr = UTF8ToString(value);
        try {
            window.localStorage.setItem(keyStr, valueStr);
            console.log('Saved to localStorage:', keyStr);
        } catch (e) {
            console.error('Failed to save to localStorage:', e);
        }
    },

    SendResultsToServer: function (jsonContent) {
        var jsonStr = UTF8ToString(jsonContent);

        try {
            // HTTP POST로 서버에 결과 전송
            fetch('http://localhost:8000/benchmark_results', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: jsonStr
            })
            .then(response => response.json())
            .then(data => {
                console.log('Results sent to server:', data);
            })
            .catch(error => {
                console.error('Failed to send results:', error);
            });
        } catch (e) {
            console.error('Failed to send results to server:', e);
        }
    }
});
