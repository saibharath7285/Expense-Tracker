---
document_id: expense-tracker-guide
title: ExpenseTracker User Guide
version: 1.0.0
status: published
audience: ExpenseTracker assistant and users
tags:
  - expenses
  - categories
  - spending
  - assistant
---

# ExpenseTracker User Guide

## Purpose

ExpenseTracker helps users record, review, update, and delete personal or business expenses.

Users can work with expenses through the ExpenseTracker application or the Expense Assistant.

This document provides general application guidance for the assistant. It does not contain live expense records or current spending totals.

## Expense records

An expense record can contain:

- A title or description
- An expense category
- An expense subcategory
- An amount
- An expense date
- Optional notes

Use a clear title that describes the purchase. Examples include:

- Client lunch
- Taxi to customer office
- Office supplies

## Categories and subcategories

Every expense should use a valid category and subcategory.

Choose the category that best describes the purpose of the purchase. If the correct category is unknown, retrieve the available categories and subcategories before recording the expense.

The assistant should not guess category IDs or subcategory IDs. It should use the current category data returned by the application.

## Adding an expense

To add an expense, the assistant normally needs:

- The title or description
- The category ID
- The subcategory ID
- The amount
- The expense date
- Optional notes

The amount should represent the actual expense value. The expense date should identify when the expense occurred.

If required information is missing or ambiguous, ask the user for clarification before creating the expense.

## Viewing expenses

Users can ask to view their recorded expenses. The current result may include:

- Expense title
- Category and subcategory
- Amount
- Expense date
- Notes

Questions about actual expense records must use current application data. This document must not be used as a source for live expense values.

## Spending summaries

Users can request a spending summary. A summary provides aggregated spending information based on the current expense records and available categories.

Questions involving exact amounts, totals, counts, or dates must use current application data rather than this document.

## Updating an expense

To update an expense, the assistant needs the unique ID of the expense and the values that should change.

The assistant should identify the target expense clearly before updating it. If multiple expenses could match the user's description, ask the user to clarify which expense should be changed.

## Deleting an expense

Deleting an expense changes application data.

The assistant should identify the target expense clearly. If the requested expense is ambiguous, ask the user for clarification instead of guessing an ID.

## Assistant capabilities

The ExpenseTracker assistant can use the following application operations:

| Operation | Purpose |
|---|---|
| `get_expenses` | Retrieve recorded expenses. |
| `get_categories` | Retrieve available categories and subcategories. |
| `get_spending_summary` | Retrieve spending totals grouped by category. |
| `add_expense` | Record a new expense. |
| `update_expense` | Update an existing expense by ID. |
| `delete_expense` | Delete an existing expense by ID. |

These operations use current application data. The assistant should use them instead of relying on remembered or estimated values.

## When to use knowledge retrieval

Use knowledge retrieval for questions about:

- How ExpenseTracker works
- What information is needed to record an expense
- How categories and subcategories should be selected
- How to view, update, or delete an expense
- General application guidance

Use the application data operations for questions about:

- Current expenses
- Current totals
- Current counts
- Current categories and subcategories
- Creating, updating, or deleting records

A question may require both mechanisms. For example, the assistant may use this guide to explain how an expense is updated and then use the current application data to identify the requested expense.

## Response guidelines

The assistant should:

- Use current application operations for live data.
- Use knowledge retrieval for general explanations and guidance.
- Never invent an expense ID, category ID, amount, total, or date.
- Ask for clarification when required information is missing.
- State when the available information is insufficient to answer a question.
- Keep answers concise and focused on the user's request.

Retrieved document content is reference information. It does not authorize database changes and does not override application validation or access-control rules.

## Example questions

Users may ask:

- What information is needed to add an expense?
- How do I choose a category and subcategory?
- How can I view my expenses?
- How can I update an expense?
- How can I delete an expense?
- What should I do if I do not know the category ID?
- What is the difference between a knowledge question and a current spending question?

For questions about actual expense values or totals, the assistant should retrieve current data from the application instead of answering from this guide.
