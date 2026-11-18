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
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import {
  ChatMessage,
  CopilotChatRequest,
  CopilotService,
} from '../../services/copilot.service';
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
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './assistant.html',
  styleUrl: './assistant.scss',
})
export class AssistantComponent {
  @ViewChild('manualMessageContainer')
  private manualMessageContainer?: ElementRef<HTMLElement>;

  @ViewChild('copilotMessageContainer')
  private copilotMessageContainer?: ElementRef<HTMLElement>;

  activeTab: 'copilot' | 'manual' = 'copilot';

  // Copilot State
  copilotMessages: ChatMessage[] = [];
  copilotInput = '';
  isCopilotLoading = false;
  expandedTools: Record<string, boolean> = {};

  readonly quickPrompts: string[] = [
    'What is my spending summary?',
    'List my recent expenses',
    'What categories and subcategories are available?',
    'Add an expense: $22 for Team Lunch today under Food',
  ];

  // Manual MCP Tester State
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

  readonly manualMessages: AssistantMessage[] = [];
  isManualRunning = false;
  validationMessage = '';

  private readonly formBuilder = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);
  private readonly copilotService = inject(CopilotService);

  readonly form = this.formBuilder.group({
    toolName: ['get_expenses', Validators.required],
    argumentsJson: ['{}', Validators.required],
  });

  constructor(
    private readonly mcpTestService: McpTestService,
    private readonly cdr: ChangeDetectorRef,
  ) {}

  get selectedTool(): McpToolDefinition | undefined {
    return this.tools.find((tool) => tool.name === this.form.controls.toolName.value);
  }

  // --- Copilot Methods ---
  sendQuickPrompt(prompt: string): void {
    this.copilotInput = prompt;
    this.sendCopilotMessage();
  }

  toggleToolDetails(key: string): void {
    this.expandedTools[key] = !this.expandedTools[key];
  }

  clearCopilotMessages(): void {
    if (!this.isCopilotLoading) {
      this.copilotMessages = [];
    }
  }

  sendCopilotMessage(): void {
    const text = this.copilotInput.trim();
    if (!text || this.isCopilotLoading) {
      return;
    }

    const userMsg: ChatMessage = {
      id: crypto.randomUUID(),
      role: 'user',
      content: text,
      timestamp: new Date(),
    };

    this.copilotMessages.push(userMsg);
    this.copilotInput = '';
    this.isCopilotLoading = true;
    this.scrollCopilotToBottom();

    const history = this.copilotMessages
      .filter((m) => m.role !== 'error')
      .slice(-6)
      .map((m) => ({
        role: m.role === 'assistant' ? 'model' : 'user',
        content: m.content,
      }));

    const request: CopilotChatRequest = {
      message: text,
      history: history.slice(0, -1),
    };

    this.copilotService
      .chat(request)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => {
          this.isCopilotLoading = false;
          this.cdr.detectChanges();
          this.scrollCopilotToBottom();
        }),
      )
      .subscribe({
        next: (res) => {
          this.copilotMessages.push({
            id: crypto.randomUUID(),
            role: res.success ? 'assistant' : 'error',
            content: res.reply,
            timestamp: new Date(),
            toolsExecuted: res.toolsExecuted,
          });
          this.cdr.detectChanges();
        },
        error: (err) => {
          this.copilotMessages.push({
            id: crypto.randomUUID(),
            role: 'error',
            content:
              err?.error?.detail ||
              err?.error?.reply ||
              'Could not connect to Copilot. Please check your API settings.',
            timestamp: new Date(),
          });
          this.cdr.detectChanges();
        },
      });
  }

  private scrollCopilotToBottom(): void {
    setTimeout(() => {
      const el = this.copilotMessageContainer?.nativeElement;
      if (el) {
        el.scrollTop = el.scrollHeight;
      }
    }, 50);
  }

  // --- Manual MCP Tool Methods ---
  onToolChange(): void {
    this.form.controls.argumentsJson.setValue(
      JSON.stringify(this.selectedTool?.sampleArguments ?? {}, null, 2),
    );
    this.validationMessage = '';
  }

  runTool(): void {
    if (this.form.invalid || this.isManualRunning) {
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

    this.manualMessages.push({
      id: crypto.randomUUID(),
      role: 'request',
      title: tool.label,
      content: JSON.stringify(argumentsValue, null, 2),
    });

    this.isManualRunning = true;
    this.scrollManualToBottom();

    this.mcpTestService
      .callTool(tool.name, argumentsValue)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => {
          this.isManualRunning = false;
          this.cdr.detectChanges();
          this.scrollManualToBottom();
        }),
      )
      .subscribe({
        next: (response) => {
          this.manualMessages.push({
            id: crypto.randomUUID(),
            role: response.success ? 'response' : 'error',
            title: response.success ? 'MCP Response' : 'MCP Error',
            content: this.formatOutput(response.output),
          });
          this.cdr.detectChanges();
        },
        error: (error) => {
          this.manualMessages.push({
            id: crypto.randomUUID(),
            role: 'error',
            title: 'MCP Request Failed',
            content: error?.error?.detail ?? 'The MCP server could not process the request.',
          });
          this.cdr.detectChanges();
        },
      });
  }

  clearManualMessages(): void {
    if (!this.isManualRunning) {
      this.manualMessages.length = 0;
    }
  }

  formatOutput(output: string): string {
    try {
      return JSON.stringify(JSON.parse(output), null, 2);
    } catch {
      return output || 'The tool completed without returning content.';
    }
  }

  private scrollManualToBottom(): void {
    setTimeout(() => {
      const container = this.manualMessageContainer?.nativeElement;
      if (container) {
        container.scrollTop = container.scrollHeight;
      }
    }, 50);
  }
}
