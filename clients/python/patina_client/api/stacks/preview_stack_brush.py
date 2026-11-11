from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx2

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.problem_details import ProblemDetails
from ...models.stack_brush import StackBrush
from ...models.stack_brush_preview import StackBrushPreview
from typing import cast



def _get_kwargs(
    id: str,
    *,
    body: StackBrush,

) -> dict[str, Any]:
    headers: dict[str, Any] = {}


    

    

    _kwargs: dict[str, Any] = {
        "method": "post",
        "url": "/api/v1/stacks/{id}/brush-preview".format(id=quote(str(id), safe=""),),
    }

    _kwargs["json"] = body.to_dict()

    headers["Content-Type"] = "application/json"

    _kwargs["headers"] = headers
    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> ProblemDetails | StackBrushPreview | None:
    if response.status_code == 200:
        response_200 = StackBrushPreview.from_dict(response.json())



        return response_200

    if response.status_code == 404:
        response_404 = ProblemDetails.from_dict(response.json())



        return response_404

    if response.status_code == 422:
        response_422 = ProblemDetails.from_dict(response.json())



        return response_422

    if response.status_code == 503:
        response_503 = ProblemDetails.from_dict(response.json())



        return response_503

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> Response[ProblemDetails | StackBrushPreview]:
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
    body: StackBrush,

) -> Response[ProblemDetails | StackBrushPreview]:
    """ A picture of a brush: one stroke of it at a size of its own, white on nothing, its pressure rising
    and falling, drawn by the Worker under /previews with the alphas this stack's strokes read. A brush
    no stroke could take is refused with its reason.

    Args:
        id (str):
        body (StackBrush): A brush. Everything but the radius has the value a new brush has.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | StackBrushPreview]
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
    body: StackBrush,

) -> ProblemDetails | StackBrushPreview | None:
    """ A picture of a brush: one stroke of it at a size of its own, white on nothing, its pressure rising
    and falling, drawn by the Worker under /previews with the alphas this stack's strokes read. A brush
    no stroke could take is refused with its reason.

    Args:
        id (str):
        body (StackBrush): A brush. Everything but the radius has the value a new brush has.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | StackBrushPreview
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
    body: StackBrush,

) -> Response[ProblemDetails | StackBrushPreview]:
    """ A picture of a brush: one stroke of it at a size of its own, white on nothing, its pressure rising
    and falling, drawn by the Worker under /previews with the alphas this stack's strokes read. A brush
    no stroke could take is refused with its reason.

    Args:
        id (str):
        body (StackBrush): A brush. Everything but the radius has the value a new brush has.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | StackBrushPreview]
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
    body: StackBrush,

) -> ProblemDetails | StackBrushPreview | None:
    """ A picture of a brush: one stroke of it at a size of its own, white on nothing, its pressure rising
    and falling, drawn by the Worker under /previews with the alphas this stack's strokes read. A brush
    no stroke could take is refused with its reason.

    Args:
        id (str):
        body (StackBrush): A brush. Everything but the radius has the value a new brush has.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | StackBrushPreview
     """


    return (await asyncio_detailed(
        id=id,
client=client,
body=body,

    )).parsed
