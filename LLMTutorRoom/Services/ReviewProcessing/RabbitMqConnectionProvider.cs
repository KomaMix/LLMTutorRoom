using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace LLMTutorRoom.Services.ReviewProcessing
{
    public sealed class RabbitMqConnectionProvider : IAsyncDisposable
    {
        private readonly RabbitMqOptions _options;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private IConnection? _connection;

        public RabbitMqConnectionProvider(IOptions<RabbitMqOptions> options)
        {
            _options = options.Value;
        }

        public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
        {
            if (_connection?.IsOpen == true)
                return _connection;

            await _gate.WaitAsync(cancellationToken);
            try
            {
                if (_connection?.IsOpen == true)
                    return _connection;

                if (_connection is not null)
                    await _connection.DisposeAsync();

                var factory = new ConnectionFactory
                {
                    HostName = _options.HostName,
                    Port = _options.Port,
                    VirtualHost = _options.VirtualHost,
                    UserName = _options.UserName,
                    Password = _options.Password,
                    AutomaticRecoveryEnabled = true,
                    TopologyRecoveryEnabled = true
                };

                _connection = await factory.CreateConnectionAsync(
                    "LLMTutorRoom review pipeline",
                    cancellationToken);
                return _connection;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_connection is not null)
                await _connection.DisposeAsync();

            _gate.Dispose();
        }
    }
}
