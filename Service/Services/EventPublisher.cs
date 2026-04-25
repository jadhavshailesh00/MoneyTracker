using Budget.Events;

namespace Budget.Services
{
    public interface IEventPublisher
    {
        Task PublishAsync<T>(T @event) where T : class;
    }

    /// <summary>
    /// In-memory event publisher (for monolith).
    /// Replace with RabbitMQ/Kafka in microservices.
    /// </summary>
    public class InMemoryEventPublisher : IEventPublisher
    {
        private readonly List<Delegate> _handlers = new();

        public void Subscribe<T>(Func<T, Task> handler) where T : class
        {
            _handlers.Add(handler);
        }

        public async Task PublishAsync<T>(T @event) where T : class
        {
            foreach (var handler in _handlers)
            {
                if (handler is Func<T, Task> typedHandler)
                {
                    await typedHandler(@event);
                }
            }
        }
    }

    /// <summary>
    /// RabbitMQ event publisher (for microservices).
    /// </summary>
    public class RabbitMqEventPublisher : IEventPublisher
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<RabbitMqEventPublisher> _logger;

        public RabbitMqEventPublisher(IConfiguration configuration, ILogger<RabbitMqEventPublisher> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public Task PublishAsync<T>(T @event) where T : class
        {
            // TODO: Implement RabbitMQ publishing
            _logger.LogInformation("Publishing event: {EventType}", typeof(T).Name);
            return Task.CompletedTask;
        }
    }
}