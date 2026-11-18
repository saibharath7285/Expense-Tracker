import { CommonModule } from '@angular/common';
import {
  ChangeDetectorRef,
  Component,
  DestroyRef,
  ElementRef,
  ViewChild,
  inject,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { McpTestService } from './mcp-test.service';

interface McpToolDefinition {
  name: string;
  label: string;
  description: string;
  sampleArguments: Record<string, unknown>;
  mutatesData: boolean;
}

interface AssistantMessage {
  id: string;
  role: 'request' | 'response' | 'error';
  title: string;
  content: string;
}

@Component({
  selector: 'app-assistant',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './assistant.html',
  styleUrl: './assistant.scss',
})
export class AssistantComponent {
  @ViewChild('messageContainer')
  private messageContainer?: ElementRef<HTMLElement>;

  readonly tools: McpToolDefinition[] = [
    {
      name: 'get_expenses',
      label: 'Get Expenses',
      description: 'Retrieve all recorded expenses.',
      sampleArguments: {},
      mutatesData: false,
    },
    {
      name: 'get_categories',
      label: 'Get Categories',
      description: 'Retrieve available categories and subcategories.',
      sampleArguments: {},
      mutatesData: false,
    },
    {
      name: 'get_spending_summary',
      label: 'Get Spending Summary',
      description: 'Retrieve expense totals grouped by category.',
      sampleArguments: {},
      mutatesData: false,
    },
    {
      name: 'add_expense',
      label: 'Add Expense',
      description: 'Create a new expense record.',
      sampleArguments: {
        title: '',
        categoryId: 0,
        subCategoryId: 0,
        amount: 0,
        expenseDate: '',
        notes: '',
      },
      mutatesData: true,
    },
    {
      name: 'update_expense',
      label: 'Update Expense',
      description: 'Update an existing expense record.',
      sampleArguments: {
        id: 0,
        title: '',
        categoryId: 0,
        subCategoryId: 0,
        amount: 0,
        expenseDate: '',
        notes: '',
      },
      mutatesData: true,
    },
    {
      name: 'delete_expense',
      label: 'Delete Expense',
      description: 'Delete an expense by ID.',
      sampleArguments: {
        id: 0,
      },
      mutatesData: true,
    },
  ];

  readonly messages: AssistantMessage[] = [];

  private readonly formBuilder = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  readonly form = this.formBuilder.group({
    toolName: ['get_expenses', Validators.required],
    argumentsJson: ['{}', Validators.required],
  });

  isRunning = false;
  validationMessage = '';

  constructor(
    private readonly mcpTestService: McpTestService,
    private readonly cdr: ChangeDetectorRef,
  ) {}

  get selectedTool(): McpToolDefinition | undefined {
    return this.tools.find((tool) => tool.name === this.form.controls.toolName.value);
  }

  onToolChange(): void {
    this.form.controls.argumentsJson.setValue(
      JSON.stringify(this.selectedTool?.sampleArguments ?? {}, null, 2),
    );
    this.validationMessage = '';
  }

  runTool(): void {
    if (this.form.invalid || this.isRunning) {
      return;
    }

    this.validationMessage = '';

    let argumentsValue: Record<string, unknown>;

    try {
      const parsedValue: unknown = JSON.parse(this.form.controls.argumentsJson.value ?? '{}');

      if (parsedValue === null || Array.isArray(parsedValue) || typeof parsedValue !== 'object') {
        throw new Error('Parameters must be a JSON object.');
      }

      argumentsValue = parsedValue as Record<string, unknown>;
    } catch (error) {
      this.validationMessage =
        error instanceof Error ? error.message : 'Enter valid JSON parameters.';
      return;
    }

    const tool = this.selectedTool;
    if (!tool) {
      this.validationMessage = 'Select an MCP tool.';
      return;
    }

    if (tool.mutatesData && !window.confirm(`Run ${tool.label}? This tool changes expense data.`)) {
      return;
    }

    this.messages.push({
      id: crypto.randomUUID(),
      role: 'request',
      title: tool.label,
      content: JSON.stringify(argumentsValue, null, 2),
    });

    this.isRunning = true;
    this.scrollToLatestMessage();

    this.mcpTestService
      .callTool(tool.name, argumentsValue)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => {
          this.isRunning = false;
          this.cdr.detectChanges();
          this.scrollToLatestMessage();
        }),
      )
      .subscribe({
        next: (response) => {
          this.messages.push({
            id: crypto.randomUUID(),
            role: response.success ? 'response' : 'error',
            title: response.success ? 'MCP Response' : 'MCP Error',
            content: this.formatOutput(response.output),
          });
          this.cdr.detectChanges();
        },
        error: (error) => {
          this.messages.push({
            id: crypto.randomUUID(),
            role: 'error',
            title: 'MCP Request Failed',
            content: error?.error?.detail ?? 'The MCP server could not process the request.',
          });
          this.cdr.detectChanges();
        },
      });
  }

  clearMessages(): void {
    if (!this.isRunning) {
      this.messages.length = 0;
    }
  }

  private formatOutput(output: string): string {
    try {
      return JSON.stringify(JSON.parse(output), null, 2);
    } catch {
      return output || 'The MCP tool completed without returning content.';
    }
  }

  private scrollToLatestMessage(): void {
    setTimeout(() => {
      const container = this.messageContainer?.nativeElement;
      if (container) {
        container.scrollTop = container.scrollHeight;
      }
    });
  }
}
