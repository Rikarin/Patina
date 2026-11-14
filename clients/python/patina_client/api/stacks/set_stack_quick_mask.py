from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx2

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.problem_details import ProblemDetails
from ...models.stack_quick_mask_request import StackQuickMaskRequest
from ...models.stack_quick_mask_state import StackQuickMaskState
from typing import cast



def _get_kwargs(
    id: str,
    *,
    body: StackQuickMaskRequest,

) -> dict[str, Any]:
    headers: dict[str, Any] = {}


    

    

    _kwargs: dict[str, Any] = {
        "method": "put",
        "url": "/api/v1/stacks/{id}/quick-mask".format(id=quote(str(id), safe=""),),
    }

    _kwargs["json"] = body.to_dict()

    headers["Content-Type"] = "application/json"

    _kwargs["headers"] = headers
    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> ProblemDetails | StackQuickMaskState | None:
    if response.status_code == 200:
        response_200 = StackQuickMaskState.from_dict(response.json())



        return response_200

    if response.status_code == 404:
        response_404 = ProblemDetails.from_dict(response.json())



        return response_404

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> Response[ProblemDetails | StackQuickMaskState]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    body: StackQuickMaskRequest,

) -> Response[ProblemDetails | StackQuickMaskState]:
    """ Shows or hides the session's quick mask in the viewport, red where it is painted, or starts a new,
    empty one. A stroke paints it with quickMask paint, and is kept inside or outside it with inside or
    outside. It is the session's: no save writes it, and neither change is a step of history.

    Args:
        id (str):
        body (StackQuickMaskRequest): Shows or hides a stack's quick mask in the viewport, or
            clears it (#127).

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | StackQuickMaskState]
     """


    kwargs = _get_kwargs(
        id=id,
body=body,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    body: StackQuickMaskRequest,

) -> ProblemDetails | StackQuickMaskState | None:
    """ Shows or hides the session's quick mask in the viewport, red where it is painted, or starts a new,
    empty one. A stroke paints it with quickMask paint, and is kept inside or outside it with inside or
    outside. It is the session's: no save writes it, and neither change is a step of history.

    Args:
        id (str):
        body (StackQuickMaskRequest): Shows or hides a stack's quick mask in the viewport, or
            clears it (#127).

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | StackQuickMaskState
     """


    return sync_detailed(
        id=id,
client=client,
body=body,

    ).parsed

async def asyncio_detailed(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    body: StackQuickMaskRequest,

) -> Response[ProblemDetails | StackQuickMaskState]:
    """ Shows or hides the session's quick mask in the viewport, red where it is painted, or starts a new,
    empty one. A stroke paints it with quickMask paint, and is kept inside or outside it with inside or
    outside. It is the session's: no save writes it, and neither change is a step of history.

    Args:
        id (str):
        body (StackQuickMaskRequest): Shows or hides a stack's quick mask in the viewport, or
            clears it (#127).

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | StackQuickMaskState]
     """


    kwargs = _get_kwargs(
        id=id,
body=body,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    body: StackQuickMaskRequest,

) -> ProblemDetails | StackQuickMaskState | None:
    """ Shows or hides the session's quick mask in the viewport, red where it is painted, or starts a new,
    empty one. A stroke paints it with quickMask paint, and is kept inside or outside it with inside or
    outside. It is the session's: no save writes it, and neither change is a step of history.

    Args:
        id (str):
        body (StackQuickMaskRequest): Shows or hides a stack's quick mask in the viewport, or
            clears it (#127).

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | StackQuickMaskState
     """


    return (await asyncio_detailed(
        id=id,
client=client,
body=body,

    )).parsed
