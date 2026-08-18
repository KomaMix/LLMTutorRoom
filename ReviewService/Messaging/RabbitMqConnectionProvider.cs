using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using ReviewService.Options;

namespace ReviewService.Messaging;

public sealed class RabbitMqConnectionProvider(
    IOptions<RabbitMqOptions> options) : IAsyncDisposable
{
    private readonly RabbitMqOptions _options = options.Value;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;

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
                "ReviewService",
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
