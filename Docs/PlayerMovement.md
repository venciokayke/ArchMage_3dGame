# Movimentacao, corrida e Stamina

Unity 2022.3.62f3. PlayerMovement continua usando CharacterController, entrada Horizontal/Vertical (WASD/setas), mira independente em PlayerCombat e LeftShift para dash. Corrida usa LeftControl; nenhuma magia consome Stamina. Nao foram alterados camera, arena, inimigos, ondas, dano ou layout do HUD.

## Aplicar

1. Pare o Play Mode e aguarde terminar a compilacao. Abra a UnderdogSurvival existente.
2. Os valores de movimento/Stamina novos sao inicializados automaticamente; confira o componente PlayerMovement do Mage. Nao precisa recriar cena, arena ou inimigos.
3. Execute **Tools > Underdog > Update Player Locomotion** para fazer o ritmo da animacao acompanhar a velocidade real. O comando mostra o Controller efetivo do Mago, pede confirmacao e cria backup em `Assets/Generated/PlayerMovement_<data>/Before.controller`.
4. A configuracao altera somente `LocomotionRate` no estado Locomotion. Mantem GUID do Controller, Blend Tree direcional, clips/eventos, Dash, ataques, Hit/Death e referencias da cena. Assets KayKit originais e a cena nao sao regravados. Alteracoes nao salvas no Animator precisam ser salvas antes de executar.
5. Entre novamente em Play, clique em Iniciar e mantenha a janela Game em foco para testar teclado/mouse. Nao use Prepare Survival Game para aplicar estas mudancas.

O jogo funciona sem executar o menu, mas o Controller antigo nao ajusta o ritmo da locomocao ao Sprint. O gerador de sobrevivencia tambem passou a configurar esse parametro para futuras partidas.

## Inspector: PlayerMovement

| Campo | Inicial | Efeito |
| --- | --- | --- |
| velocidade | 5 | Velocidade normal, inclusive sem Stamina. |
| aceleracao | 40 | Resposta ao comecar a mover ou aumentar velocidade. |
| desaceleracao | 55 | Frenagem ao soltar/reduzir a entrada. |
| teclaCorrida | LeftControl | Manter pressionada para correr. |
| multiplicadorCorrida | 1.4 | Velocidade de corrida 7 com base 5. |
| staminaMaxima | 100 | Capacidade inicial/maxima. |
| consumoStamina | 18/s | Consumo apenas enquanto corre e realmente se desloca. |
| regeneracaoStamina | 25/s | Recuperacao quando nao corre. |
| atrasoRegeneracao | 0.75 s | Espera apos o ultimo consumo. |
| recuperacaoMinima | 20 | Minimo para voltar a correr apos esgotar. |
| velocidadeDash / duracaoDash / cooldownDash | 12 / 0.2 / 1 | Impulso e cooldown existentes; sem custo de Stamina. |
| desaceleracaoFimDash | 120 | Retorno curto e gradual da velocidade/direcao do dash para a entrada atual. |
| gravidade / efeitoDash | existentes | Gravidade e efeito preservados. |

StaminaAtual, StaminaEsgotada, Correndo, EstaDashando e VelocidadeAtual sao propriedades de consulta, sem nova interface. Segurar Control parado ou bloqueado contra parede nao consome folego. Ao esgotar, continua andando normalmente; segurando Control, retoma a corrida quando recuperar o minimo. O dash conserva o impulso de 2.4 unidades, seguido de um retorno suavizado curto, que pode acrescentar pequeno deslocamento conforme a entrada/friccao configurada.

## Animacoes reais reutilizadas

O Animator existente usa Idle_A, Running_A, Walking_Backwards, Running_Strafe_Left e Running_Strafe_Right; o dash usa Dodge_Forward, Dodge_Backward, Dodge_Left e Dodge_Right. Sao os clips reais ja usados pelo gerador KayKit, copiados nos assets PlayerIdle/Forward/Backward/Left/Right e PlayerDash0-3. Nao foi inventado clip de Sprint: ele reutiliza a locomocao direcional com ritmo proporcional ao deslocamento real. Na parada, o ritmo volta a 1 para nao congelar Idle.

PlayerMovement nao gira o Mago: PlayerCombat continua orientando-o para o mouse. O Blend Tree recebe a velocidade real convertida para o espaco local do personagem, preservando lateral/tras. A taxa extra afeta somente Locomotion; camadas Upper Body/Feedback e velocidades de ataque/Dash/Hit/Death continuam existentes.

## Diagnostico do bloqueio de comandos

Na captura do Editor, a partida ainda estava em Inicio e WaveManager mantinha movimento/combate desabilitados, como previsto antes de iniciar. O teste fisico da cena real deslocou o Mage 2.5 unidades sem bloqueio por colisao ou Animator. Foi reproduzida uma fragilidade do HUD: listeners de runtime eram ligados apenas em Start e nao voltavam apos sua perda/reativacao. Agora OnEnable liga e OnDisable remove somente seus proprios callbacks, sem mudar layout, estado das ondas ou outros listeners.

Se reaparecer: em Play na tela inicial, **Tools > Underdog > Check Match Start (Play Mode)** verifica tambem a religacao do botao, inicio, liberacao de comandos e primeiro spawn. Morte continua bloqueando comandos intencionalmente. Os testes automatizados injetam entradas e eventos; nao comprovam a entrega das teclas/mouse fisicos pelo Editor.

## Validacao

- **Compilacao:** todos os scripts de runtime/Editor contra assemblies Unity 2022.3.62f3; somente os avisos anteriores NavigationStatic/ItemEffect.
- **Play Mode Unity, copia da cena real:** inicio/spawn, religacao do HUD, deslocamento do Mage, Sprint durante LancarMagia por Animation Event, LocomotionRate=1.4 e deslocamento durante Hit.
- **Play Mode Unity, fixture fisica:** aceleracao/frenagem, diagonal normalizada, consumo/atraso/regeneracao, esgotamento e recuperacao minima, dash com/sem Stamina, cooldown, retorno para andar/correr, parede com dt=0.3, ausencia de consumo quando bloqueado, Hit e morte. Repetir por **Tools > Underdog > Check Player Movement (Play Mode)**; cria/remove atores temporarios perto de (100,0,100), preferencialmente numa cena vazia.
- **Edit Mode Unity, Controller real copiado:** backup/GUID preservados, somente Locomotion parametrizado; clips/eventos e estados Dash/ataques/Hit/Death preservados. Relatorios em Docs/Validation/PlayerMovement-PlayMode.xml e PlayerMovement-Animator-EditMode.xml.

**Pendentes no Editor interativo:** WASD/mouse fisicos na janela Game; sensacao de aceleracao/frenagem; corrida nas quatro direcoes e diagonal; esgotar/recarregar segurando Control; soltar/reverter direcao apos dash; magias primaria/secundaria/Q durante Sprint; colisao com paredes, props e grupos mistos de inimigos; morte/reinicio e cinco ondas completas. Observar deslizamento dos pes e ajustar velocidades/ritmo caso necessario. Nao houve benchmark de CPU/GPU nem aprovacao humana do novo game feel.
