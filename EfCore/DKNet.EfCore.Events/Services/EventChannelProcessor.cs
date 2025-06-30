using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DKNet.EfCore.Events.Services;

/// <summary>
/// Background service that processes events from a channel asynchronously
/// </summary>
internal sealed class EventChannelProcessor : BackgroundService
{
    private readonly Channel<QueuedEventBatch> _eventChannel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EventChannelProcessor> _logger;

    public EventChannelProcessor(
        Channel<QueuedEventBatch> eventChannel,
        IServiceProvider serviceProvider,
        ILogger<EventChannelProcessor> logger)
    {
        _eventChannel = eventChannel;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Event Channel Processor started");

        await foreach (var eventBatch in _eventChannel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessEventBatchAsync(eventBatch, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing event batch");
            }
        }

        _logger.LogInformation("Event Channel Processor stopped");
    }

    private async Task ProcessEventBatchAsync(QueuedEventBatch eventBatch, CancellationToken cancellationToken)
    {
        // Create a new scope for processing this batch of events
        // This ensures proper DbContext lifecycle management
        using var scope = _serviceProvider.CreateScope();
        var eventPublishers = scope.ServiceProvider.GetServices<IEventPublisher>();

        _logger.LogDebug("Processing {EventCount} events from {EntityCount} entities", 
            eventBatch.Events.Sum(e => e.Events.Count), 
            eventBatch.Events.Count);

        // Process all events from all entities
        var publishTasks = from entityEventItem in eventBatch.Events
                          from eventPublisher in eventPublishers
                          select eventPublisher.PublishAllAsync(entityEventItem.Events, cancellationToken);

        try
        {
            await Task.WhenAll(publishTasks);
            _logger.LogDebug("Successfully processed event batch");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process some events in batch");
            // Don't rethrow - we want to continue processing other batches
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Event Channel Processor");
        
        // Complete the channel to stop accepting new events
        _eventChannel.Writer.Complete();
        
        await base.StopAsync(cancellationToken);
    }
}

/// <summary>
/// Represents a batch of events to be processed asynchronously
/// </summary>
internal sealed record QueuedEventBatch(IReadOnlyList<EntityEventItem> Events);