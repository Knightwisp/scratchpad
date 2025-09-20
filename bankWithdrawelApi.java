package com.poorbank.controllers;

import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.web.bind.annotation.*;
import software.amazon.awssdk.regions.Region;
import software.amazon.awssdk.services.sns.SnsClient;
import software.amazon.awssdk.services.sns.model.PublishRequest;
import software.amazon.awssdk.services.sns.model.PublishResponse;
import java.math.BigDecimal;

@RestController
@RequestMapping("/bank")
public class BankAccountController {
    // Note: Hard-coded dependencies, no proper DI
    @Autowired
    private JdbcTemplate jdbcTemplate;
    private SnsClient snsClient;

    public BankAccountController() {
        // Hard-coded AWS client configuration
        this.snsClient = SnsClient.builder()
            .region(Region.US_WEST_2)
            .build();
    }

    @PostMapping("/withdraw")
    public String withdraw(@RequestParam("accountId") Long accountId, @RequestParam("amount") BigDecimal amount) {
        // Check current balance - Note: Race condition potential here
        String sql = "SELECT balance FROM accounts WHERE id = ?";
        BigDecimal currentBalance = jdbcTemplate.queryForObject(sql, new Object[]{accountId}, BigDecimal.class);

        if (currentBalance != null && currentBalance.compareTo(amount) >= 0) {
            // Update balance - Note: No transaction management
            sql = "UPDATE accounts SET balance = balance - ? WHERE id = ?";
            int rowsAffected = jdbcTemplate.update(sql, amount, accountId);

            if (rowsAffected > 0) {
                // After a successful withdrawal, publish event to SNS
                // Note: Event publishing not part of transaction
                WithdrawalEvent event = new WithdrawalEvent(amount, accountId, "SUCCESSFUL");
                String eventJson = event.toJson();

                // Hard-coded SNS topic ARN
                String snsTopicArn = "arn:aws:sns:us-west-2:123456789012:WithdrawalEvents";
                PublishRequest publishRequest = PublishRequest.builder()
                    .message(eventJson)
                    .topicArn(snsTopicArn)
                    .build();

                // Fire and forget publish - no error handling
                PublishResponse publishResponse = snsClient.publish(publishRequest);
                return "Withdrawal successful";
            } else {
                return "Withdrawal failed";
            }
        } else {
            return "Insufficient funds for withdrawal";
        }
    }
}

class WithdrawalEvent {
    private BigDecimal amount;
    private Long accountId;
    private String status;

    public WithdrawalEvent(BigDecimal amount, Long accountId, String status) {
        this.amount = amount;
        this.accountId = accountId;
        this.status = status;
    }

    public BigDecimal getAmount() {
        return amount;
    }

    public Long getAccountId() {
        return accountId;
    }

    public String getStatus() {
        return status;
    }

    // Basic JSON conversion - no proper error handling
    public String toJson() {
        return String.format("{\"amount\":\"%s\",\"accountId\":%d,\"status\":\"%s\"}", 
            amount, accountId, status);
    }
}
