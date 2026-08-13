curl -X POST "https://localhost:5001/api/webhooks/subscriptions"  -H "Content-Type: application/json" -d '{"url": "https://myapp.example.com/webhooks/orders", "events": ["order.created", "order.updated"],"description": "Order processing webhook","maxRetries": 5,"retryDelayMinutes": 2 }'

# Trigger a webhook event
curl -X POST "https://localhost:5001/api/webhooks/events" \
  -H "Content-Type: application/json" \
  -d '{
    "eventType": "order.created",
    "source": "OrderService",
    "data": {
      "orderId": "12345",
      "customerId": "67890",
      "amount": 99.99,
      "currency": "USD"
    }
  }'

# Receive a webhook (with authentication)
curl -X POST "https://localhost:5001/webhooks/receive/generic" \
  -H "Content-Type: application/json" \
  -H "X-API-Key: your-api-key" \
  -H "X-Webhook-Signature: sha256=abc123..." \
  -d '{"message": "Hello webhook!"}'