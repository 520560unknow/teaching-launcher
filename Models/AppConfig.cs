using System.Text.Json.Serialization;

namespace StartMode.Models
{
    public class AppConfig
    {
        [JsonPropertyName("webAppUrl")]
        public string WebAppUrl { get; set; } = "http://localhost:8080/teacher/dashboard";

        [JsonPropertyName("serverHealthUrl")]
        public string ServerHealthUrl { get; set; } = "http://localhost:8080";

        [JsonPropertyName("serverBaseUrl")]
        public string ServerBaseUrl { get; set; } = "http://localhost:8080";

        [JsonPropertyName("webSocketBaseUrl")]
        public string WebSocketBaseUrl { get; set; } = "ws://localhost:8080";

        [JsonPropertyName("springBootJarPath")]
        public string SpringBootJarPath { get; set; } = "app/firestDemo-0.0.1-SNAPSHOT.jar";

        [JsonPropertyName("javaExePath")]
        public string JavaExePath { get; set; } = "app/jre8/bin/java.exe";

        [JsonPropertyName("llamaServerPath")]
        public string LlamaServerPath { get; set; } = "app/llama/llama-server.exe";

        [JsonPropertyName("llamaModelPath")]
        public string LlamaModelPath { get; set; } = "";

        [JsonPropertyName("llamaBaseUrl")]
        public string LlamaBaseUrl { get; set; } = "http://127.0.0.1:8081";

        [JsonPropertyName("llamaPort")]
        public int LlamaPort { get; set; } = 8081;

        [JsonPropertyName("enableSimulatedMessages")]
        public bool EnableSimulatedMessages { get; set; }

        [JsonPropertyName("pollingIntervalSeconds")]
        public int PollingIntervalSeconds { get; set; } = 5;

        [JsonPropertyName("floatingBallSize")]
        public int FloatingBallSize { get; set; } = 80;

        [JsonPropertyName("isTopmost")]
        public bool IsTopmost { get; set; } = true;

        [JsonPropertyName("maxCachedMessages")]
        public int MaxCachedMessages { get; set; } = 500;

        [JsonPropertyName("theme")]
        public string Theme { get; set; } = "Light";

        [JsonPropertyName("floatingBallLeft")]
        public double FloatingBallLeft { get; set; } = -1;

        [JsonPropertyName("floatingBallTop")]
        public double FloatingBallTop { get; set; } = -1;
    }
}
